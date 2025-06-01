#!/usr/bin/env python3
"""
Strands Tool for F# BenchmarkDotNet Docker Execution
"""

import docker
import re
import tempfile
import shutil
import platform
from typing import Optional, List, Dict, Any
from pathlib import Path

class FSharpBenchmark:
    """Runs F# benchmarks in Docker and extracts key metrics"""

    def __init__(self):
        self.docker_image = "mcr.microsoft.com/dotnet/sdk:9.0"

        # Windows-specific Docker client initialization
        if platform.system() == "Windows":
            # For Windows, we need to set environment variable first
            import os
            os.environ['DOCKER_HOST'] = 'npipe:////./pipe/docker_engine'
            self.client = docker.DockerClient(base_url='tcp://localhost:2375')
        else:
            self.client = docker.from_env()

    def run(self, project_path: str, args: Optional[List[str]] = None) -> Dict[str, Any]:
        """Execute F# benchmark project in Docker and parse results"""

        # Create Dockerfile content
        dockerfile = f"""
        FROM {self.docker_image}
        WORKDIR /app
        COPY . .
        RUN dotnet restore
        """

        # Create temporary directory and copy project
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            project_copy = temp_path / "project"

            # Copy project files (Windows-safe)
            if platform.system() == "Windows":
                import os
                os.makedirs(project_copy, exist_ok=True)
                for item in Path(project_path).iterdir():
                    if item.is_file():
                        shutil.copy2(str(item), str(project_copy))
                    elif item.is_dir() and item.name not in ['.git', 'bin', 'obj']:
                        shutil.copytree(str(item), str(project_copy / item.name))
            else:
                shutil.copytree(project_path, project_copy)

            # Write Dockerfile
            (project_copy / "Dockerfile").write_text(dockerfile)

            # Build image with Windows path handling
            build_path = str(project_copy).replace('\\', '/') if platform.system() == "Windows" else str(project_copy)
            image_tag = f"fsharp-benchmark:temp-{id(self)}"
            print("build path:", build_path)
            image, build_logs = self.client.images.build(
                path=build_path,
                tag=image_tag,
                rm=True,
                forcerm=True
            )

            # full_command = ["dotnet", "run", "tests.txt", "expected.txt", "-c", "Release"]
            if args:
                full_command = ["dotnet", "run"] + args + ["-c", "Release"]
            else:
                full_command = ["dotnet", "run", "--benchmark", "--no-build", "-c", "Release"]

            # Run container
            container = self.client.containers.run(
                image_tag,
                command=full_command,
                stdout=True,
                stderr=True,
                detach=False
            )
            # for line in container.logs(stream=True):
            #     print(line.strip())
            # Decode output
            output = container.decode('utf-8') if isinstance(container, bytes) else str(container)

            # Parse output
            parsed = self._parse_benchmarks(output)

            # Cleanup image
            self.client.images.remove(image_tag, force=True)

            return {
                "success": True,
                "output": output,
                "error": "",
                "benchmarks": parsed,
                "insights": self._generate_insights(parsed)
            }

    def _parse_benchmarks(self, output: str) -> list:
        """Extract benchmark results from BenchmarkDotNet output"""
        benchmarks = []

        lines = output.split('\n')
        in_table = False

        for line in lines:
            if "| Method" in line and "Mean" in line:
                in_table = True
                continue

            if in_table and line.startswith('|') and not line.startswith('|--'):
                parts = [p.strip() for p in line.split('|') if p.strip()]
                if len(parts) >= 3 and parts[0] not in ["Method", ""]:
                    mean_match = re.search(r'([\d.]+)\s*(\w+)', parts[1])
                    if mean_match:
                        benchmarks.append({
                            "method": parts[0],
                            "mean": float(mean_match.group(1)),
                            "unit": mean_match.group(2)
                        })

        return benchmarks

    def _generate_insights(self, benchmarks: list) -> [float,list]:
        """Generate simple insights from benchmark results"""
        if not benchmarks:
            return ["No benchmarks found in output"]

        insights = [f"✅ Found {len(benchmarks)} benchmarks"]

        sorted_benchmarks = sorted(benchmarks, key=lambda x: x["mean"])
        fastest = sorted_benchmarks[0]
        slowest = sorted_benchmarks[-1]

        insights.append(f"⚡ Fastest: {fastest['method']} ({fastest['mean']} {fastest['unit']})")
        insights.append(f"🐌 Slowest: {slowest['method']} ({slowest['mean']} {slowest['unit']})")

        if fastest["mean"] > 0:
            ratio = slowest["mean"] / fastest["mean"]
            if ratio > 2:
                insights.append(f"📊 {slowest['method']} is {ratio:.1f}x slower than {fastest['method']}")

        return fastest, insights

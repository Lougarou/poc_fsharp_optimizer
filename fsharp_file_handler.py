def read_file(dir, fsharp_file: str) -> str:
    """
    returns the content of the F# file as a string from a directory.
    """
    try:
        #TODO: handle multiple operating systems
        with open(f"{dir}\\{fsharp_file}", "r", encoding="utf-8") as file:
            return file.read()
    except FileNotFoundError:
        raise ValueError(f"File {fsharp_file} not found.")
    except Exception as e:
        raise ValueError(f"Error reading file {fsharp_file}: {str(e)}") from e

def create_project(source_folder, target_folder):
    import shutil
    import os

    try:
        if not os.path.exists(source_folder):
            raise FileNotFoundError(f"Source folder does not exist: {source_folder}")

        if not os.path.isdir(source_folder):
            raise NotADirectoryError(f"Source is not a directory: {source_folder}")

        print(f"Creating project from {source_folder} to {target_folder}")
        shutil.copytree(source_folder, target_folder, dirs_exist_ok=True)

    except FileNotFoundError as e:
        print(f"Error: {e}")
    except PermissionError as e:
        print(f"Permission error: {e}")
    except Exception as e:
        print(f"Unexpected error: {type(e).__name__}: {e}")

def write_file(dir, fsharp_file: str, content: str):
    """
    writes the content to the F# file in a directory.
    """
    try:
        with open(f"{dir}\\{fsharp_file}", "w", encoding="utf-8") as file:
            file.write(content)
            print(f"File {fsharp_file} written successfully in {dir}.")
    except FileNotFoundError:
        raise ValueError(f"File {fsharp_file} not found.")
    except Exception as e:
        raise ValueError(f"Error writing file {fsharp_file}: {str(e)}") from e

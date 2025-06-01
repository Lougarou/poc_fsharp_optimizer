open System
open System.IO

open System
open System.Collections.Generic

// Data structures
type GeoCoordinate = {
    Latitude: float
    Longitude: float
    Altitude: float
}

type FuelType = 
    | E95
    | E98
    | Diesel
    | E85
    | Electric
    | Hydrogen

type FuelPump = {
    Id: Guid
    Brand: string
    Address: string
    Coordinates: GeoCoordinate
    AvailableFuels: FuelType Set
}

type RoadConnection = {
    FromCity: string
    ToCity: string
    Distance: float
}

type City = {
    Id: Guid
    Name: string
    Country: string
    Coordinates: GeoCoordinate
    FuelPumps: FuelPump array
    ConnectedCities: City ref list
}

type TestCase = {
    Name: string
    Cities: (string * FuelType list array) list
    Connections: RoadConnection list
    StartCity: string
}

type ExpectedResult = {
    TestName: string
    E95Cities: string list
    TraversalOrder: string list option
}

// Parsing functions
let parseFuelType (fuelStr: string) =
    match fuelStr.Trim().ToUpper() with
    | "E95" -> Some E95
    | "E98" -> Some E98
    | "DIESEL" -> Some Diesel
    | "E85" -> Some E85
    | "ELECTRIC" -> Some Electric
    | "HYDROGEN" -> Some Hydrogen
    | _ -> None

let parseAllTests (content: string) =
    let lines = content.Split('\n') |> Array.map (fun l -> l.Trim()) |> Array.filter (fun l -> not (String.IsNullOrWhiteSpace(l)) && not (l.StartsWith("#")))
    
    let mutable tests = []
    let mutable currentTest = None
    let mutable currentSection = ""
    let mutable cities = []
    let mutable connections = []
    let mutable startCity = None
    
    let finalizeTest () =
        match currentTest, startCity with
        | Some name, Some start ->
            tests <- { Name = name; Cities = List.rev cities; Connections = List.rev connections; StartCity = start } :: tests
            cities <- []
            connections <- []
            startCity <- None
            currentTest <- None
        | _ -> ()
    
    for line in lines do
        if line.StartsWith("[TEST:") && line.EndsWith("]") then
            finalizeTest()
            currentTest <- Some (line.Substring(6, line.Length - 7).Trim())
            currentSection <- "TEST"
        elif line.StartsWith("[") && line.EndsWith("]") then
            currentSection <- line.Substring(1, line.Length - 2).ToUpper()
        else
            match currentSection with
            | "CITIES" ->
                // Format: CityName: FuelType1,FuelType2|FuelType3,FuelType4
                let parts = line.Split(':')
                if parts.Length = 2 then
                    let cityName = parts.[0].Trim()
                    let pumpsStr = parts.[1].Trim()
                    let pumps = 
                        pumpsStr.Split('|') 
                        |> Array.map (fun pumpStr ->
                            pumpStr.Split(',')
                            |> Array.choose parseFuelType
                            |> Array.toList
                        )
                    cities <- (cityName, pumps) :: cities
                    
            | "CONNECTIONS" ->
                // Format: FromCity -> ToCity : Distance
                let parts = line.Split([|"->"; ":"|], StringSplitOptions.None)
                if parts.Length >= 2 then
                    let fromCity = parts.[0].Trim()
                    let toCity = parts.[1].Trim()
                    let distance = 
                        if parts.Length >= 3 then
                            match Double.TryParse(parts.[2].Trim()) with
                            | true, d -> d
                            | _ -> 10.0
                        else 10.0
                    connections <- { FromCity = fromCity; ToCity = toCity; Distance = distance } :: connections
                    
            | "START" ->
                startCity <- Some line
            | _ -> ()
    
    finalizeTest()
    List.rev tests

let parseAllExpectedResults (content: string) =
    let lines = content.Split('\n') |> Array.map (fun l -> l.Trim()) |> Array.filter (fun l -> not (String.IsNullOrWhiteSpace(l)) && not (l.StartsWith("#")))
    
    let mutable results = []
    let mutable currentTest = None
    let mutable currentSection = ""
    let mutable e95Cities = []
    let mutable traversalOrder = None
    
    let finalizeResult () =
        match currentTest with
        | Some name ->
            results <- { TestName = name; E95Cities = List.rev e95Cities; TraversalOrder = traversalOrder } :: results
            e95Cities <- []
            traversalOrder <- None
            currentTest <- None
        | _ -> ()
    
    for line in lines do
        if line.StartsWith("[TEST:") && line.EndsWith("]") then
            finalizeResult()
            currentTest <- Some (line.Substring(6, line.Length - 7).Trim())
            currentSection <- "TEST"
        elif line.StartsWith("[") && line.EndsWith("]") then
            currentSection <- line.Substring(1, line.Length - 2).ToUpper()
        else
            match currentSection with
            | "E95_CITIES" ->
                e95Cities <- line :: e95Cities
                
            | "TRAVERSAL_ORDER" ->
                let cities = line.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.toList
                traversalOrder <- Some cities
            | _ -> ()
    
    finalizeResult()
    List.rev results

// Graph creation
let createCity name fuelPumpConfigs =
    {
        Id = Guid.NewGuid()
        Name = name
        Country = "TestCountry"
        Coordinates = { Latitude = 0.0; Longitude = 0.0; Altitude = 0.0 }
        FuelPumps = fuelPumpConfigs |> Array.mapi (fun i fuels ->
            {
                Id = Guid.NewGuid()
                Brand = "TestBrand"
                Address = sprintf "Pump %d" i
                Coordinates = { Latitude = 0.0; Longitude = 0.0; Altitude = 0.0 }
                AvailableFuels = Set.ofList fuels
            }
        )
        ConnectedCities = []
    }

let buildGraph cities connections =
    // Create cities
    let cityMap = 
        cities 
        |> List.map (fun (name, pumps) -> name, createCity name pumps)
        |> Map.ofList
    
    // Create mutable references for cities
    let cityRefs = 
        cityMap 
        |> Map.map (fun _ city -> ref city)
    
    // Connect cities based on connections
    for conn in connections do
        match Map.tryFind conn.FromCity cityRefs, Map.tryFind conn.ToCity cityRefs with
        | Some fromRef, Some toRef ->
            // Update connections (bidirectional)
            fromRef.Value <- { fromRef.Value with ConnectedCities = toRef :: fromRef.Value.ConnectedCities }
            toRef.Value <- { toRef.Value with ConnectedCities = fromRef :: toRef.Value.ConnectedCities }
        | _ -> failwithf "Invalid connection: %s -> %s" conn.FromCity conn.ToCity
    
    cityRefs

// Graph traversal algorithm
let breadthFirstSearch (startCity: City) (predicate: City -> bool) =
    let visited = HashSet<Guid>()
    let queue = Queue<City>()
    let results = ResizeArray<City>()
    let visitOrder = ResizeArray<string>()
    
    queue.Enqueue(startCity)
    visited.Add(startCity.Id) |> ignore
    visitOrder.Add(startCity.Name)
    
    while queue.Count > 0 do
        let current = queue.Dequeue()
        
        if predicate current then
            results.Add(current)
        
        // Sort connections by city name for deterministic traversal
        let sortedConnections = 
            current.ConnectedCities 
            |> List.sortBy (fun cityRef -> (!cityRef).Name)
        
        for connectedRef in sortedConnections do
            let connected = !connectedRef
            if not (visited.Contains(connected.Id)) then
                visited.Add(connected.Id) |> ignore
                queue.Enqueue(connected)
                visitOrder.Add(connected.Name)
    
    results |> Seq.toList, visitOrder |> Seq.toList

// Search predicates
let hasE95FuelPump (city: City) =
    city.FuelPumps |> Array.exists (fun pump -> pump.AvailableFuels.Contains(E95))

// Main search function
let findCitiesWithE95 (testCase: TestCase) =
    let cityRefs = buildGraph testCase.Cities testCase.Connections
    
    match Map.tryFind testCase.StartCity cityRefs with
    | None -> 
        Error (sprintf "Start city '%s' not found" testCase.StartCity)
    | Some startRef ->
        let results, traversalOrder = breadthFirstSearch startRef.Value hasE95FuelPump
        let e95Cities = results |> List.map (fun c -> c.Name) |> List.sort
        Ok (e95Cities, traversalOrder)

// Result comparison
let compareResults actual expected =
    let actualSorted = actual |> List.sort
    let expectedSorted = expected |> List.sort
    actualSorted = expectedSorted

// Run a single test
let runSingleTest (test: TestCase) (expected: ExpectedResult) =
    printfn "\n=== Test: %s ===" test.Name
    printfn "Cities: %d, Connections: %d" test.Cities.Length test.Connections.Length
    printfn "Starting from: %s" test.StartCity
    
    match findCitiesWithE95 test with
    | Error msg ->
        printfn "Error: %s" msg
        false
    | Ok (e95Cities, traversalOrder) ->
        // Compare results
        let e95Match = compareResults e95Cities expected.E95Cities
        let traversalMatch = 
            match expected.TraversalOrder with
            | None -> true
            | Some expectedOrder -> traversalOrder = expectedOrder
        
        // Display results
        printfn "Cities with E95: %A" e95Cities
        if expected.TraversalOrder.IsSome then
            printfn "Traversal order: %A" traversalOrder
        
        // Display validation
        printfn "E95 cities match: %s" (if e95Match then "✓ PASS" else "✗ FAIL")
        if not e95Match then
            printfn "  Expected: %A" (expected.E95Cities |> List.sort)
            printfn "  Actual: %A" (e95Cities |> List.sort)
        
        if expected.TraversalOrder.IsSome then
            printfn "Traversal match: %s" (if traversalMatch then "✓ PASS" else "✗ FAIL")
            if not traversalMatch then
                printfn "  Expected: %A" expected.TraversalOrder.Value
                printfn "  Actual: %A" traversalOrder
        
        e95Match && traversalMatch

// Run all tests
let runAllTests testsFile expectedFile =
    printfn "Graph Traversal E95 Finder"
    printfn "========================="
    
    // Read files
    let testsContent = File.ReadAllText(testsFile)
    let expectedContent = File.ReadAllText(expectedFile)
    
    // Parse all tests and expected results
    let tests = parseAllTests testsContent
    let expectedResults = parseAllExpectedResults expectedContent
    
    printfn "Loaded %d tests" tests.Length
    
    // Match tests with expected results
    let mutable passCount = 0
    let mutable totalCount = 0
    
    for test in tests do
        match expectedResults |> List.tryFind (fun e -> e.TestName = test.Name) with
        | None ->
            printfn "\n⚠️  No expected results found for test: %s" test.Name
        | Some expected ->
            totalCount <- totalCount + 1
            if runSingleTest test expected then
                passCount <- passCount + 1
    
    // Summary
    printfn "\n========================="
    printfn "Summary: %d/%d tests passed" passCount totalCount
    printfn "========================="
    
    passCount = totalCount

// Benchmark support
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Jobs

[<SimpleJob(RuntimeMoniker.Net90, warmupCount = 3, iterationCount = 5)>]
[<MemoryDiagnoser>]
type GraphBenchmark() =
    
    member val TestCases: TestCase list = [] with get, set
    
    [<GlobalSetup>]
    member this.Setup() =
        // Load tests from default file or create sample tests
        if File.Exists("tests.txt") then
            let content = File.ReadAllText("tests.txt")
            this.TestCases <- parseAllTests content
        else
            // Create some default test cases for benchmarking
            this.TestCases <- [
                {
                    Name = "Small Graph"
                    Cities = [
                        "A", [| [E95; Diesel] |]
                        "B", [| [Diesel] |]
                        "C", [| [E95; E98] |]
                    ]
                    Connections = [
                        { FromCity = "A"; ToCity = "B"; Distance = 10.0 }
                        { FromCity = "B"; ToCity = "C"; Distance = 20.0 }
                    ]
                    StartCity = "A"
                }
                {
                    Name = "Large Graph"
                    Cities = 
                        [0..49] |> List.map (fun i -> 
                            sprintf "City%d" i, 
                            if i % 5 = 0 then [| [E95; Diesel] |] else [| [Diesel] |]
                        )
                    Connections = 
                        [0..48] |> List.collect (fun i ->
                            [
                                { FromCity = sprintf "City%d" i; ToCity = sprintf "City%d" (i+1); Distance = 10.0 }
                                if i % 10 = 0 && i + 10 < 50 then
                                    { FromCity = sprintf "City%d" i; ToCity = sprintf "City%d" (i+10); Distance = 50.0 }
                            ]
                        )
                    StartCity = "City0"
                }
            ]
    
    [<Benchmark>]
    member this.BenchmarkAllTests() =
        let mutable totalFound = 0
        for test in this.TestCases do
            match findCitiesWithE95 test with
            | Ok (cities, _) -> totalFound <- totalFound + cities.Length
            | Error _ -> ()
        totalFound
    
    [<Benchmark>]
    member this.BenchmarkLargestTest() =
        let largestTest = this.TestCases |> List.maxBy (fun t -> t.Cities.Length)
        match findCitiesWithE95 largestTest with
        | Ok (cities, _) -> cities.Length
        | Error _ -> 0

[<EntryPoint>]
let main argv =
    match argv with
    | [| testsFile; expectedFile |] ->
        if File.Exists(testsFile) && File.Exists(expectedFile) then
            let success = runAllTests testsFile expectedFile
            if success then 0 else 1
        else
            printfn "Error: Input files not found"
            printfn "Usage: dotnet run <tests-file> <expected-file>"
            1
    
    | [| "--benchmark" |] ->
        printfn "Running benchmarks..."
        BenchmarkRunner.Run<GraphBenchmark>() |> ignore
        0
    
    | [| "--help" |] | [| "-h" |] ->
        printfn "Graph Traversal E95 Finder"
        printfn "========================="
        printfn ""
        printfn "Usage:"
        printfn "  dotnet run <tests-file> <expected-file>  # Run tests and validate"
        printfn "  dotnet run --benchmark                    # Run performance benchmarks"
        printfn "  dotnet run --help                         # Show this help"
        printfn ""
        printfn "Tests file format:"
        printfn "  [TEST: Test Name]"
        printfn "  [CITIES]"
        printfn "  CityName: Fuel1,Fuel2|Fuel3,Fuel4  # | separates pumps"
        printfn "  [CONNECTIONS]"
        printfn "  City1 -> City2 : Distance"
        printfn "  [START]"
        printfn "  StartCityName"
        printfn ""
        printfn "Expected file format:"
        printfn "  [TEST: Test Name]"
        printfn "  [E95_CITIES]"
        printfn "  City1"
        printfn "  City2"
        printfn "  [TRAVERSAL_ORDER]  # Optional"
        printfn "  City1, City2, City3"
        printfn ""
        printfn "Benchmark notes:"
        printfn "  - Benchmarks will use tests.txt if available"
        printfn "  - Otherwise, it creates default test cases"
        printfn "  - Run in Release mode for accurate results: dotnet run -c Release -- --benchmark"
        0
        
    | _ ->
        printfn "Usage: dotnet run <tests-file> <expected-file>"
        printfn "   or: dotnet run --benchmark"
        printfn "   or: dotnet run --help"
        1
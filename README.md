# MiniGameRouter

<img width="1085" alt="image" src="https://github.com/user-attachments/assets/94b33485-665f-4cd0-8869-25420b19fa92">

## Projects

- `MiniGameRouter`: Register center, REST API implementation
- `MiniGameRouter.SDK`: SDK for clients
- `MiniGameRouter.SideCar`: Default sidecar server implementation
- `MiniGameRouter.Benchmark`: Benchmark project for various core functions and logic
- `MiniGameRouter.PressureTest`: A demo fake client used to run the pressure test for the Register Center

## Build

Currently, all major projects fully support standlone & docker/K8s env deployment.

### Build and run in standlone mode

1. Install .NET 8 SDK from [Microsoft - Dotnet](https://dot.net)
2. Go to any of the project dir listed above
3. Run:
    ```bash
    dotnet publish -c Release
    ```
4. Go to `/[Project_Dir]/bin/Release/net8.0`, you should see the build output and the executable of the project.

### Deploy in Docker (Recommended)

1. Setup required docker env on server
2. Go to repo's root dir **(NOT PROJECT'S SUB DIR)**
3. Run:
    ```bash
    docker build . -f [PROJECT_DIR]/Dockerfile -t minigamerouter/[NAME]:latest
    ```
4. Use docker run to start the container.

## Docker run commands

### `MiniGameRouter`

```bash
docker run -d \
    -p [EXPOSE_PORT]:80 \
    --name [NAME] \
    --add-host=host.docker.internal:host-gateway \
    -e ASPNETCORE_HTTP_PORTS=80 \
    -e "ConnectionStrings:RedisCache=host.docker.internal:6379" \
    -e "ConnectionStrings:DefaultMongoConnection=mongodb://host.docker.internal:27017" \
    minigamerouter/backend:latest
```

### `MiniGameRouter.PressureTest`

```bash
docker run -d \
    --name [NAME] \
    --add-host=host.docker.internal:host-gateway \
    -e "MiniGameRouter:ConnectionString=http://host.docker.internal:[PORT]" \
    -e "PressureTest:ConnectionString=http://host.docker.internal:[PORT]" \
    minigamerouter/pressure_test:latest
```

## Advanced pressure test config tweak

The default config for the pressure test is:

```json
"PressureTest": {
    "ConnectionString": "[CONNECTION_STRING]",  // Backend Endpoint
    "ServiceCount": 10,                         // Service count
    "InstanceCount": 20,                        // Instance count,
                                                // this means how many instance under one service.
                                                // Total number of reg = ServiceCount * InstanceCount
    "RandomEndPointOps": {
      "ParallelCount": 20,                      // Number of concurrent ops
      "GetSubInstanceCount": 10,                // Instance count for get test
      "EnableRandomCreateAndDelete": true,      // Enable instance random create and delete
      "EnableRandomUpdate": true,               // Enable instance random edit
      "EnableRandomGet": true                   // Enable instance random get
    },
    "RandomDynamicMappingOps": {
      "ParallelCount": 200,                     // Number of concurrent ops
      "MappingCount": 10000,                    // Total mappings register count
      "EnableRandomCreateAndGet": true          // Enable mapping random create and get
    }
}
```
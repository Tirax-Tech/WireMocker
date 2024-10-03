# WireMocker: HTTP Mock Server

WireMocker is a standalone user interface built using WireMock.Net and Blazor technology. It allows you to create and manage HTTP mock servers effortlessly through a web-based UI. Designed to function as a standalone application, WireMocker can run multiple instances simultaneously without any conflicts.

Inspired by [Smocker, an HTTP mock server](https://smocker.dev), WireMocker aims to provide an intuitive experience for mocking APIs during development and testing.

## Features
* **Intuitive Web UI**: Manage your mock mappings through a user-friendly interface.
* **Separate Admin and API Endpoints**: Enhances security and organization by separating administration from the mock server.
* **Multi-Instance Support**: Run multiple instances concurrently without interference.
* **Built with Blazor**: Leverages modern Blazor technology for a seamless user experience.
* **WireMock.Net Integration**: Utilizes WireMock.Net for robust mock server capabilities.

## Getting Started
### Prerequisites

* .NET 8.0 SDK or higher installed on your machine.

### Installation

Clone the repository:

```bash
git clone https://github.com/yourusername/WireMocker.git
cd WireMocker
```

### Running the Application

Start the application with the following command:

```bash
dotnet run
```

By default, WireMocker exposes two HTTP endpoints:

* **Administrator UI**: Accessible at https://localhost:6625/ (Port `6625` or mnemonic `MOCK`).
* **API Mock Server**: Accessible at http://localhost:9091/ (Port `9091`).

### Usage
#### Accessing the Administrator UI

Open your web browser and navigate to:

```
https://localhost:6625/
```

Here you can:

* Create, edit, and delete mock mappings.
* View request logs.
* Configure global settings.

#### Setting Up Mappings via API

In WireMock.Net, you typically set up mappings by POSTing to http://localhost:9091/__admin/mappings/. In WireMocker, the admin endpoints have been moved to the Administrator UI endpoint. To set up mappings via API, use:

```
POST https://localhost:6625/__admin/mappings/
```

This separation ensures that administrative operations are isolated from the mock server endpoint.

#### Using the API Mock Server

Point your application under test to the API mock server endpoint:

```
http://localhost:9091/
```

All mock responses defined in the Administrator UI will be served from this endpoint.

#### Running Multiple Instances

To run multiple instances of WireMocker, ensure that each instance uses different ports to avoid conflicts. You can configure the ports by modifying the application settings or passing command-line arguments.

## Contributing

Contributions are welcome! If you'd like to contribute, please:

1. Fork the repository.
2. Create a new branch for your feature or bug fix.
3. Submit a pull request detailing your changes.

## License

This project is licensed under the Apache 2.0 License. See the LICENSE file for details.
Acknowledgements

* [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net) for the foundational mock server functionality.
* [Smocker](https://smocker.dev) for inspiring this project.

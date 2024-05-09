# CLI Tools for .NET Applications

Welcome to the CLI Tools repository, where we've developed a robust set of command-line interfaces for managing .NET applications efficiently. This repository contains a series of examples demonstrating various capabilities from basic command handling to complex integrations with frameworks like Blazor for web-based management interfaces.

## Features

- **Command Execution**: Execute actions through simple or complex commands with full support for arguments and options.
- **Dependency Injection**: Utilizes scoped and singleton services efficiently across command executions, compatible with both single execution and REPL (Read-Eval-Print Loop) modes.
- **Error Handling**: Robust error management with default and customizable error handling strategies.
- **Integration with ASP.NET Core**: Examples showing how to integrate and manage an ASP.NET Core web host within CLI commands.
- **Blazor Integration**: Advanced samples demonstrating how to embed CLI in a Blazor application for interactive command execution directly from the browser.
- **Modular Design**: Easy to extend and customize, supporting a wide range of applications and use cases.

## Getting Started

### Prerequisites

Ensure you have the following installed:
- **.NET 8.0 SDK** or later
- An IDE such as Visual Studio, VS Code, or JetBrains Rider

### Setting Up Your Project

1. **Create a New Console Application**:
   ```bash
   dotnet new console -n MyCLIApp
   cd MyCLIApp
   ```

2. **Add the CoreVar.CommandLineInterface NuGet Package**:
   ```bash
   dotnet add package CoreVar.CommandLineInterface
   ```

3. **Explore the Examples**:
   Navigate to the examples within this repository to see how to implement various CLI functionalities.

### Running the Examples

To run any of the examples, use the following command from within the project directory:

```bash
dotnet run
```

## Usage

Each folder in the repository is structured to contain separate projects with their own specific examples:
- **Basic CLI Operations**: Simple commands and configurations.
- **Dependency Injection**: Demonstrates scoped and singleton services.
- **Blazor Integration**: Shows how to embed CLI within a Blazor application.

Refer to the README.md files within each project folder for detailed usage instructions.

## Contributing

We welcome contributions from the community! Whether you're fixing a bug, improving documentation, or proposing a new feature, we appreciate your help. Please pull a request with your changes directly.

### Pull Requests

1. Fork the repository.
2. Create your feature branch (`git checkout -b feature/AmazingFeature`).
3. Commit your changes (`git commit -am 'Add some AmazingFeature'`).
4. Push to the branch (`git push origin feature/AmazingFeature`).
5. Open a pull request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Special thanks to the .NET community for continuous support and feedback.
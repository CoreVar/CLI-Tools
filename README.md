# CLI Tools for .NET Applications

Welcome to the CLI Tools repository, where we've developed a robust set of .NET tools for building .NET command line applications efficiently. This repository contains a series of examples demonstrating various capabilities from basic command handling to complex integrations with frameworks like Blazor for web-based management interfaces.

## Features

- **Simple authoring**: Build commands with a compact fluent API or source-generated component classes.
- **Rich binding**: Scalars, enums, dates, paths, URIs, arrays, lists, repeated options, variadic arguments, environment variables, configuration, defaults, and response files.
- **Production CLI behavior**: Validation, middleware, cancellation, aliases, deprecation, hidden commands, global options, typo suggestions, help, version output, and shell completion.
- **Automation friendly**: A host-backed test harness, JSON/JSONL helpers, and generated Markdown reference documentation.
- **Dependency Injection**: Utilizes scoped and singleton services efficiently across command executions, compatible with both single execution and REPL (Read-Eval-Print Loop) modes.
- **Error Handling**: Robust error management with default and customizable error handling strategies.
- **Integration with ASP.NET Core**: Examples showing how to integrate and manage an ASP.NET Core web host within CLI commands.
- **Blazor Integration**: Advanced samples demonstrating how to embed CLI in a Blazor application for interactive command execution directly from the browser.
- **Modular Design**: Easy to extend and customize, supporting a wide range of applications and use cases.
- **.NET 8 and .NET 10**: One package targets both the current LTS generations. Major package releases track .NET's even-numbered LTS cadence.
- **AOT Compatible**: The core CLI runtime and source-generated components support trimming and Native AOT. (Blazor examples retain the platform's own publishing constraints.)

## Getting Started

### Prerequisites

Ensure you have the following installed:
- **.NET 8.0 SDK** or **.NET 10.0 SDK**
- An IDE such as Visual Studio, VS Code, or JetBrains Rider

### Setting Up a New Project
1. **Install Project Templates (once per machine)**
   ```bash
   dotnet new install CoreVar.CommandLineInterface.Templates
   ``` 
2. **Create a New Simple CLI Project**
    - Simple CLI
      ```bash
      dotnet new cli-simple -n MyCli
      ``` 
    - OR Components CLI
      ```bash
      dotnet new cli-components -n MyCli
      ```

### Add Tools to an Existing Project

1. **Add the CoreVar.CommandLineInterface NuGet Package**:
   ```bash
   dotnet add package CoreVar.CommandLineInterface
   ```

2. **Modify Program.cs**
    ```
    using CoreVar.CommandLineInterface;

    await CliApp.RunAsync(cliApp =>
    {
        cliApp
            .Command("start", startCommand =>
            {
                startCommand
                    .OnExecute(() => Console.WriteLine("Service started"));
            })
            .Command("stop", stopCommand =>
            {
                stopCommand
                    .OnExecute(() => Console.WriteLine("Service stopped."));
            });
    });
    ```

    This is the simplest example of how to build a CLI application. See the [v10 guide](docs/v10.md) for validation, middleware, binding, completion, component metadata, prompting, testing, and documentation generation.

5. **Explore the Examples**:
   Navigate to the examples within this repository to see how to implement various CLI functionalities.

### Running the Examples

To run any of the examples, run/debug from Visual Studio 2022 or use the following command from within the project directory:

```bash
dotnet run
```

## Usage

Each folder in the repository is structured to contain separate projects with their own specific examples:
- **Basic CLI Operations**: Simple commands and configurations.
- **Dependency Injection**: Demonstrates scoped and singleton services.
- **Blazor Integration**: Shows how to embed CLI within a Blazor application.

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

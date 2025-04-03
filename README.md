# Revit Report Creator

![CI](https://github.com/PrestonII/aps-report-creator/workflows/CI/badge.svg)

A Design Automation for Revit application that creates reports with images from external sources.

## Features

- Creates PDF reports from Revit models
- Downloads and includes images from external APIs with authentication
- Organizes images on sheets with proper layout
- Exports to PDF

## Development

### Prerequisites

- .NET 6.0 SDK
- Revit API references

### Building
```bash
dotnet build Design-Automation_Version/ReportCreatorApp.sln
```
```bash
dotnet test Design-Automation_Version/ReportCreatorApp.Tests/ReportCreatorApp.Tests.csproj
```


## License

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](LICENSE) file for full details.
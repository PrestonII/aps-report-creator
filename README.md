# Revit Report Creator

![CI](https://github.com/PrestonII/aps-report-creator/workflows/CI/badge.svg)
[![.net](https://img.shields.io/badge/.net-4.8-green.svg)](http://www.microsoft.com/en-us/download/details.aspx?id=30653)
[![Design Automation](https://img.shields.io/badge/Design%20Automation-v3-green.svg)](https://aps.autodesk.com/en/docs/design-automation/v3/developers_guide/overview/)
[![visual studio](https://img.shields.io/badge/Visual%20Studio-2022-green.svg)](https://www.visualstudio.com/)
[![revit](https://img.shields.io/badge/revit-2023-red.svg)](https://www.autodesk.com/products/revit/overview/)

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
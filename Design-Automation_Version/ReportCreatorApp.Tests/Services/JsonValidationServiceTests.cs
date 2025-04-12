using ipx.revit.reports.Models;
using ipx.revit.reports.Services;

using Newtonsoft.Json;

using NUnit.Framework;

namespace ipx.revit.reports.Tests.Services
{
    [TestFixture]
    public class JsonValidationServiceTests
    {
        private JsonValidationService _service = null!;
        private string _testDataPath = null!;

        [SetUp]
        public void Setup()
        {
            _service = new JsonValidationService();
            _testDataPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");

            // Create test directory if it doesn't exist
            if (!Directory.Exists(_testDataPath))
            {
                Directory.CreateDirectory(_testDataPath);
            }
        }

        [Test]
        public void ValidateAndParseProjectData_ValidJson_ReturnsProjectData()
        {
            // Arrange
            var jsonContent = @"{
                'ProjectName': 'Test Project',
                'ProjectNumber': 'TP-001',
                'ReportType': 'Asset Report',
                'ViewTypes': ['FloorPlan', 'Section'],
                'MaxViews': 10,
                'OutputFileName': 'test_report.pdf',
                'Environment': 'development',
                'Authentication': {
                    'Username': 'test_user',
                    'Password': 'test_pass'
                }
            }";

            var jsonPath = Path.Combine(_testDataPath, "valid_project.json");
            File.WriteAllText(jsonPath, jsonContent);

            // Act
            var result = _service.ValidateAndParseProjectData(jsonPath);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ProjectName, Is.EqualTo("Test Project"));
            Assert.That(result.ProjectNumber, Is.EqualTo("TP-001"));
            Assert.That(result.ReportType, Is.EqualTo("Asset Report"));
            Assert.That(result.MaxViews, Is.EqualTo(10));
            Assert.That(result.OutputFileName, Is.EqualTo("test_report.pdf"));
            Assert.That(result.Environment, Is.EqualTo("development"));
            Assert.That(result.Authentication!.Username, Is.EqualTo("test_user"));
            Assert.That(result.Authentication.Password, Is.EqualTo("test_pass"));

            // Cleanup
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }
        }

        [Test]
        public void ValidateAndParseProjectData_InvalidJson_ReturnsNull()
        {
            // Arrange
            var jsonContent = "{ invalid json content }";
            var jsonPath = Path.Combine(_testDataPath, "invalid_project.json");
            File.WriteAllText(jsonPath, jsonContent);

            // Act
            var result = _service.ValidateAndParseProjectData(jsonPath);

            // Assert
            Assert.That(result, Is.Null);

            // Cleanup
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }
        }

        [Test]
        public void ValidateAndParseProjectData_NonexistentFile_ReturnsNull()
        {
            // Act
            var result = _service.ValidateAndParseProjectData("nonexistent.json");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ValidateAndParseProjectData_ValidJsonWithViewTypes_ParsesViewTypesCorrectly()
        {
            // Arrange
            var expectedViewTypes = new List<string>
            {
                "FloorPlan",
                "Section",
                "ThreeD"
            };

            var jsonContent = $@"{{
                'ProjectName': 'Test Project',
                'ProjectNumber': 'TP-001',
                'ReportType': 'Asset Report',
                'ViewTypes': ['FloorPlan', 'Section', 'ThreeD'],
                'MaxViews': 10,
                'OutputFileName': 'test_report.pdf',
                'Environment': 'development'
            }}";

            var jsonPath = Path.Combine(_testDataPath, "valid_project_with_viewtypes.json");
            File.WriteAllText(jsonPath, jsonContent);

            // Act
            var result = _service.ValidateAndParseProjectData(jsonPath);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ViewTypes, Is.Not.Null);
            Assert.That(result.ViewTypes, Is.EquivalentTo(expectedViewTypes));

            // Cleanup
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }
        }

        [Test]
        public void ValidateAuthentication_ValidCredentials_ReturnsTrue()
        {
            // Arrange
            var projectData = new ProjectData
            {
                Authentication = new AuthenticationInfo
                {
                    Username = "test_user",
                    Password = "test_password"
                }
            };

            // Act
            var result = _service.ValidateAuthentication(projectData);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void ValidateAuthentication_NullAuthentication_ReturnsFalse()
        {
            // Arrange
            var projectData = new ProjectData
            {
                Authentication = null
            };

            // Act
            var result = _service.ValidateAuthentication(projectData);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ValidateAuthentication_EmptyUsername_ReturnsFalse()
        {
            // Arrange
            var projectData = new ProjectData
            {
                Authentication = new AuthenticationInfo
                {
                    Username = "",
                    Password = "test_password"
                }
            };

            // Act
            var result = _service.ValidateAuthentication(projectData);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ValidateAuthentication_EmptyPassword_ReturnsFalse()
        {
            // Arrange
            var projectData = new ProjectData
            {
                Authentication = new AuthenticationInfo
                {
                    Username = "test_user",
                    Password = ""
                }
            };

            // Act
            var result = _service.ValidateAuthentication(projectData);

            // Assert
            Assert.That(result, Is.False);
        }

        private string CreateValidProjectDataJson()
        {
            var projectData = new ProjectData
            {
                ProjectName = "Test Project",
                ProjectNumber = "2023-001",
                ReportType = "AssetReport",
                ViewTypes = new List<string>
                {
                    "FloorPlan",
                    "Elevation"
                },
                MaxViews = 10,
                OutputFileName = "TestReport",
                Environment = "development",
                Authentication = new AuthenticationInfo
                {
                    Username = "test_user",
                    Password = "test_password"
                },
                ImageData = new List<AssetData>
                {
                    new()
                    {
                        AssetId = "asset1",
                        Project = "project1",
                        AssetType = "image",
                        AssetName = "Test Image 1",
                        AssetUrl = "https://example.com/image1.png"
                    },
                    new()
                    {
                        AssetId = "asset2",
                        Project = "project1",
                        AssetType = "image",
                        AssetName = "Test Image 2",
                        AssetUrl = "https://example.com/image2.png"
                    }
                }
            };

            return JsonConvert.SerializeObject(projectData, Formatting.Indented);
        }

        [TearDown]
        public void Cleanup()
        {
            // Clean up test directory if it exists
            if (Directory.Exists(_testDataPath))
            {
                Directory.Delete(_testDataPath, true);
            }
        }
    }
}
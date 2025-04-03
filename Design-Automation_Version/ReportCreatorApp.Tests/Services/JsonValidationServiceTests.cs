using System;
using System.IO;
using NUnit.Framework;
using ipx.revit.reports.Models;
using ipx.revit.reports.Services;
using Newtonsoft.Json;

namespace ipx.revit.reports.Tests.Services
{
    [TestFixture]
    public class JsonValidationServiceTests
    {
        private JsonValidationService _service;
        private string _testDataPath;

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
            var validJson = CreateValidProjectDataJson();
            var jsonPath = Path.Combine(_testDataPath, "valid_project_data.json");
            File.WriteAllText(jsonPath, validJson);

            // Act
            var result = _service.ValidateAndParseProjectData(jsonPath);

            // Assert
            Assert.NotNull(result);
            Assert.AreEqual("Test Project", result.ProjectName);
            Assert.AreEqual("2023-001", result.ProjectNumber);
            Assert.AreEqual("AssetReport", result.ReportType);
            Assert.AreEqual(2, result.ImageData.Count);
            Assert.AreEqual("test_user", result.Authentication.Username);
        }

        [Test]
        public void ValidateAndParseProjectData_FileNotFound_ReturnsNull()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDataPath, "non_existent_file.json");

            // Act
            var result = _service.ValidateAndParseProjectData(nonExistentPath);

            // Assert
            Assert.Null(result);
        }

        [Test]
        public void ValidateAndParseProjectData_InvalidJson_ReturnsNull()
        {
            // Arrange
            var invalidJson = "{ this is not valid JSON }";
            var jsonPath = Path.Combine(_testDataPath, "invalid_json.json");
            File.WriteAllText(jsonPath, invalidJson);

            // Act
            var result = _service.ValidateAndParseProjectData(jsonPath);

            // Assert
            Assert.Null(result);
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
            Assert.IsTrue(result);
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
            Assert.IsFalse(result);
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
            Assert.IsFalse(result);
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
            Assert.IsFalse(result);
        }

        private string CreateValidProjectDataJson()
        {
            var projectData = new ProjectData
            {
                ProjectName = "Test Project",
                ProjectNumber = "2023-001",
                ReportType = "AssetReport",
                ViewTypes = new List<Autodesk.Revit.DB.ViewType> 
                { 
                    Autodesk.Revit.DB.ViewType.FloorPlan, 
                    Autodesk.Revit.DB.ViewType.Elevation 
                },
                MaxViews = 10,
                OutputFileName = "TestReport",
                Authentication = new AuthenticationInfo
                {
                    Username = "test_user",
                    Password = "test_password"
                },
                ImageData = new List<AssetData>
                {
                    new AssetData
                    {
                        AssetId = "asset1",
                        Project = "project1",
                        AssetType = "image",
                        AssetName = "Test Image 1",
                        AssetUrl = "https://example.com/image1.png"
                    },
                    new AssetData
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
    }
} 
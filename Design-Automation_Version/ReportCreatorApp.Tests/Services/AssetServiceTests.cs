using System;
using System.IO;
using NUnit.Framework;
using ipx.revit.reports.Models;
using ipx.revit.reports.Services;
using Newtonsoft.Json;

namespace ipx.revit.reports.Tests.Services
{
    [TestFixture]
    public class AssetServiceTests
    {
        private AssetService _service;
        private string _testDataPath;

        [SetUp]
        public void Setup()
        {
            _service = new AssetService();
            _testDataPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");
            
            // Create test directory if it doesn't exist
            if (!Directory.Exists(_testDataPath))
            {
                Directory.CreateDirectory(_testDataPath);
            }
        }

        [Test]
        public void ParseAssetJson_ValidJson_ReturnsAssetList()
        {
            // Arrange
            var assets = new List<AssetData>
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
            };
            
            var jsonPath = Path.Combine(_testDataPath, "valid_assets.json");
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(assets, Formatting.Indented));

            // Act
            var result = _service.ParseAssetJson(jsonPath);

            // Assert
            Assert.NotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("asset1", result[0].AssetId);
            Assert.AreEqual("Test Image 2", result[1].AssetName);
        }

        [Test]
        public void ParseAssetJson_FileNotFound_ReturnsEmptyList()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDataPath, "non_existent_file.json");

            // Act
            var result = _service.ParseAssetJson(nonExistentPath);

            // Assert
            Assert.NotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ParseAssetJson_InvalidJson_ReturnsEmptyList()
        {
            // Arrange
            var invalidJson = "{ this is not valid JSON }";
            var jsonPath = Path.Combine(_testDataPath, "invalid_assets.json");
            File.WriteAllText(jsonPath, invalidJson);

            // Act
            var result = _service.ParseAssetJson(jsonPath);

            // Assert
            Assert.NotNull(result);
            Assert.AreEqual(0, result.Count);
        }
    }
} 
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ipx.revit.reports.Services;
using ipx.revit.reports.Models;

namespace ipx.revit.reports.Tests.Services
{
    [TestFixture]
    public class AssetServiceTests
    {
        private AssetService _service = null!;
        private string _testDataPath = null!;

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
            var jsonContent = @"[
                {
                    'AssetId': 'ASSET-001',
                    'Project': 'Test Project',
                    'AssetType': 'image',
                    'ImageSubtype': 'photo',
                    'AssetName': 'Test Asset',
                    'AssetUrl': 'https://example.com/image.jpg'
                }
            ]";
            
            var jsonPath = Path.Combine(_testDataPath, "valid_assets.json");
            File.WriteAllText(jsonPath, jsonContent);

            // Act
            var result = _service.ParseAssetJson(jsonPath);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].AssetId, Is.EqualTo("ASSET-001"));
            Assert.That(result[0].Project, Is.EqualTo("Test Project"));
            Assert.That(result[0].AssetType, Is.EqualTo("image"));
            Assert.That(result[0].ImageSubtype, Is.EqualTo("photo"));
            Assert.That(result[0].AssetName, Is.EqualTo("Test Asset"));
            Assert.That(result[0].AssetUrl, Is.EqualTo("https://example.com/image.jpg"));

            // Cleanup
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }
        }

        [Test]
        public void ParseAssetJson_InvalidJson_ReturnsEmptyList()
        {
            // Arrange
            var jsonContent = "{ invalid json content }";
            var jsonPath = Path.Combine(_testDataPath, "invalid_assets.json");
            File.WriteAllText(jsonPath, jsonContent);

            // Act
            var result = _service.ParseAssetJson(jsonPath);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);

            // Cleanup
            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }
        }

        [Test]
        public void ParseAssetJson_FileNotFound_ReturnsEmptyList()
        {
            // Act
            var result = _service.ParseAssetJson("nonexistent.json");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
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
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ipx.revit.reports.Services;
using Moq;
using Moq.Protected;

namespace ipx.revit.reports.Tests.Services
{
    [TestFixture]
    public class FileServiceTests
    {
        private string _testDataPath;

        [SetUp]
        public void Setup()
        {
            _testDataPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");
            
            // Create test directory if it doesn't exist
            if (!Directory.Exists(_testDataPath))
            {
                Directory.CreateDirectory(_testDataPath);
            }
        }

        [Test]
        public async Task DownloadImageAsync_ValidUrl_DownloadsImage()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }) // PNG file header
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            
            // Use reflection to create a FileService with our mocked HttpClient
            var fileService = new FileService("test_user", "test_password");
            var fieldInfo = typeof(FileService).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fieldInfo.SetValue(fileService, httpClient);

            var url = "https://example.com/image.png";
            var destinationFolder = Path.Combine(_testDataPath, "downloads");

            // Act
            var result = await fileService.DownloadImageAsync(url, destinationFolder);

            // Assert
            Assert.IsTrue(File.Exists(result));
            Assert.IsTrue(result.Contains(destinationFolder));
            Assert.IsTrue(result.EndsWith("image.png"));

            // Cleanup
            if (File.Exists(result))
            {
                File.Delete(result);
            }
        }

        [Test]
        public void DownloadImage_ValidUrl_DownloadsImage()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }) // PNG file header
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            
            // Use reflection to create a FileService with our mocked HttpClient
            var fileService = new FileService("test_user", "test_password");
            var fieldInfo = typeof(FileService).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fieldInfo.SetValue(fileService, httpClient);

            var url = "https://example.com/image.png";
            var destinationFolder = Path.Combine(_testDataPath, "downloads");

            // Act
            var result = fileService.DownloadImage(url, destinationFolder);

            // Assert
            Assert.IsTrue(File.Exists(result));
            Assert.IsTrue(result.Contains(destinationFolder));
            Assert.IsTrue(result.EndsWith("image.png"));

            // Cleanup
            if (File.Exists(result))
            {
                File.Delete(result);
            }
        }

        [Test]
        public void DownloadImage_HttpError_ThrowsException()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("Not Found")
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            
            // Use reflection to create a FileService with our mocked HttpClient
            var fileService = new FileService("test_user", "test_password");
            var fieldInfo = typeof(FileService).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fieldInfo.SetValue(fileService, httpClient);

            var url = "https://example.com/nonexistent.png";
            var destinationFolder = Path.Combine(_testDataPath, "downloads");

            // Act & Assert
            Assert.Throws<Exception>(() => fileService.DownloadImage(url, destinationFolder));
        }

        [Test]
        public void DownloadImage_AuthenticationHeaderIsSet()
        {
            // Arrange
            HttpRequestMessage capturedRequest = null;
            
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((request, token) => capturedRequest = request)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }) // PNG file header
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            
            // Use reflection to create a FileService with our mocked HttpClient
            var fileService = new FileService("test_user", "test_password");
            var fieldInfo = typeof(FileService).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            fieldInfo.SetValue(fileService, httpClient);

            var url = "https://example.com/image.png";
            var destinationFolder = Path.Combine(_testDataPath, "downloads");

            // Act
            var result = fileService.DownloadImage(url, destinationFolder);

            // Assert
            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.Headers.Authorization);
            Assert.AreEqual("Basic", capturedRequest.Headers.Authorization.Scheme);
            
            // The value should be base64 encoded "test_user:test_password"
            string expectedToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("test_user:test_password"));
            Assert.AreEqual(expectedToken, capturedRequest.Headers.Authorization.Parameter);

            // Cleanup
            if (File.Exists(result))
            {
                File.Delete(result);
            }
        }
    }
} 
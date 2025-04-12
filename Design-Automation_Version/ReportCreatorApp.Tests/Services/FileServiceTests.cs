using System.Net;

using ipx.revit.reports.Services;

using Moq;
using Moq.Protected;

using NUnit.Framework;

namespace ipx.revit.reports.Tests.Services
{
    [TestFixture]
    public class FileServiceTests
    {
        private string _testDataPath = null!;

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
        public async Task DownloadFileAsync_ValidUrl_DownloadsFile()
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
            fieldInfo?.SetValue(fileService, httpClient);

            var url = "https://example.com/image.png";
            var destinationPath = Path.Combine(_testDataPath, "downloads", "image.png");

            // Act
            var result = await fileService.DownloadFileAsync(url, destinationPath);

            // Assert
            Assert.That(File.Exists(result), Is.True);
            Assert.That(result.Contains(_testDataPath), Is.True);
            Assert.That(result.EndsWith("image.png"), Is.True);

            // Cleanup
            if (File.Exists(result))
            {
                File.Delete(result);
            }
        }

        [Test]
        public void DownloadFile_HttpError_ThrowsException()
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
            fieldInfo?.SetValue(fileService, httpClient);

            var url = "https://example.com/nonexistent.png";
            var destinationPath = Path.Combine(_testDataPath, "downloads", "nonexistent.png");

            // Act & Assert
            Assert.ThrowsAsync<HttpRequestException>(async () => await fileService.DownloadFileAsync(url, destinationPath));
        }

        [Test]
        public void DownloadFile_AuthenticationHeaderIsSet()
        {
            // Arrange
            HttpRequestMessage? capturedRequest = null;

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
            fieldInfo?.SetValue(fileService, httpClient);

            var url = "https://example.com/image.png";
            var destinationPath = Path.Combine(_testDataPath, "downloads", "image.png");

            // Act
            var task = fileService.DownloadFileAsync(url, destinationPath);
            task.Wait();

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.Headers.Authorization, Is.Not.Null);
            Assert.That(capturedRequest.Headers.Authorization!.Scheme, Is.EqualTo("Basic"));

            // The value should be base64 encoded "test_user:test_password"
            string expectedToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("test_user:test_password"));
            Assert.That(capturedRequest.Headers.Authorization.Parameter, Is.EqualTo(expectedToken));

            // Cleanup
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
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
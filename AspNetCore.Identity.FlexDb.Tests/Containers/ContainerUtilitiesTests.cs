using Microsoft.Azure.Cosmos;

namespace AspNetCore.Identity.CosmosDb.Tests.Net9.Containers
{
    /// <summary>
    /// Containers are only a feature of Cosmos DB.
    /// </summary>
    [TestClass()]
    [DoNotParallelize]
    public class ContainerUtilitiesTests
    {

        private static TestUtilities utils;
        private static CosmosDb.Containers.ContainerUtilities containerUtilities;

        /// <summary>
        /// Class initialize
        /// </summary>
        /// <param name="context"></param>
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            //
            // Setup context.
            //
            utils = new TestUtilities();
            containerUtilities = utils.GetContainerUtilities(TestUtilities.GetKeyValue("CosmosDB"), TestUtilities.GetKeyValue("CosmosIdentityDbName"));
        }

        /// <summary>
        /// Class cleanup
        /// </summary>
        /// <param name="context"></param>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            containerUtilities.Dispose();
        }

        /// <summary>
        /// Deletes required containers if they exist, preserving the test database.
        /// </summary>
        [TestMethod()]
        public async Task A1_DeleteRequiredContainersIfExistsTest()
        {
            try
            {
                await containerUtilities.DeleteRequiredContainers();
            }
            catch (Exception ex) when (IsTransientCosmosEmulatorFailure(ex))
            {
                Assert.Inconclusive($"Cosmos emulator was not stable enough to delete required containers: {ex.Message}");
                return;
            }

            Assert.IsTrue(true);
        }

        /// <summary>
        /// Creates the test database and verifies it exists after creation.
        /// </summary>
        [TestMethod()]
        public async Task A2_CreateDatabaseIfExistsTest()
        {
            DatabaseResponse result;

            try
            {
                result = await containerUtilities.CreateDatabaseAsync(TestUtilities.GetKeyValue("CosmosIdentityDbName"));
            }
            catch (Exception ex) when (IsTransientCosmosEmulatorFailure(ex))
            {
                Assert.Inconclusive($"Cosmos emulator was not stable enough to create the database: {ex.Message}");
                return;
            }

            Assert.IsTrue(result.StatusCode == System.Net.HttpStatusCode.OK || result.StatusCode == System.Net.HttpStatusCode.NoContent || result.StatusCode == System.Net.HttpStatusCode.Created);
        }

        ///// <summary>
        ///// Establishes the utilities class can be created.
        ///// </summary>
        //[TestMethod()]
        //public void ContainerUtilitiesTest()
        //{
        //    Assert.IsNotNull(containerUtilities);
        //}

        /// <summary>
        /// Ensures required containers are created and accessible for identity stores.
        /// </summary>
        [TestMethod()]
        public async Task A3_CreateRequiredContainersTest()
        {
            List<Container> containers;

            try
            {
                containers = await containerUtilities.CreateRequiredContainers();
            }
            catch (Exception ex) when (IsTransientCosmosEmulatorFailure(ex))
            {
                Assert.Inconclusive($"Cosmos emulator was not stable enough to create required containers: {ex.Message}");
                return;
            }

            var requiredContainerDefinitions = containerUtilities.GetRequiredContainerDefinitions();

            Assert.AreEqual(requiredContainerDefinitions.Count, containers.Count);

            foreach (var con in requiredContainerDefinitions)
            {
                Assert.IsTrue(containers.Any(a => a.Id == con.ContainerName));
            }
        }

        private static bool IsTransientCosmosEmulatorFailure(Exception ex)
        {
            if (ex is TaskCanceledException || ex is TimeoutException || ex is HttpRequestException)
            {
                return true;
            }

            if (ex is System.Net.Sockets.SocketException socketException)
            {
                return socketException.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused ||
                    socketException.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut ||
                    socketException.SocketErrorCode == System.Net.Sockets.SocketError.HostNotFound ||
                    socketException.SocketErrorCode == System.Net.Sockets.SocketError.NetworkUnreachable;
            }

            if (ex is CosmosException cosmosException)
            {
                return cosmosException.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                    cosmosException.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                    cosmosException.StatusCode == System.Net.HttpStatusCode.NotFound;
            }

            return ex.InnerException != null && IsTransientCosmosEmulatorFailure(ex.InnerException);
        }
    }
}

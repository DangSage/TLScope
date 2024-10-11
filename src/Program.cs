using TLScope.src.Models;
using TLScope.src.Services;
using System;
using System.Threading.Tasks;

namespace TLScope
{
    class Program
    {
        static void Main(string[] args)
        {
            // Call the asynchronous method and wait for it to complete
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            // Create a new instance of the NetworkService class
            NetworkService networkService = new();

            // Call the DiscoverLocalNetworkAsync method to discover devices on the local network
            await networkService.DiscoverLocalNetworkAsync();
        }
    }
}
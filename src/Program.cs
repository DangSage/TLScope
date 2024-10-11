
using TLScope.src.Models;
using TLScope.src.Services;
using System;

// main class of the program

namespace TLScope
{
    class Program
    {
        static void Main(string[] args)
        {
            // create a new instance of the NetworkService class
            NetworkService networkService = new();

            // call the DiscoverLocalNetwork method to discover devices on the local network
            networkService.DiscoverLocalNetwork();

            // create a new user object
            User user = new()
            {
                Username = "john_doe",
                Password = "password123",
                Email = "",
                Role = "admin",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // print the user object
            Console.WriteLine(user);
            return;
        }
    }
}
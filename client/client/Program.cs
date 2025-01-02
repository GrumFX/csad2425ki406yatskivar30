using System;
using System.IO.Ports;
using System.Linq;
class Program
{
    static void Main(string[] args)
    {
        string[] availablePorts = SerialPort.GetPortNames();
        if (availablePorts.Length == 0)
        {
            Console.WriteLine("No COM ports found!");
            return;
        }

        DisplayAvailablePorts(availablePorts);
        string selectedPort = SelectPort(availablePorts);
        const int baudRate = 9600;

        try
        {
            using (SerialPort serialPort = new SerialPort(selectedPort, baudRate))
            {

                serialPort.WriteTimeout = 1000;
                serialPort.ReadTimeout = 1000;

                Console.WriteLine($"\nConnecting to {selectedPort}...");
                serialPort.Open();
                Console.WriteLine($"Successfully connected to {selectedPort}");
                Console.WriteLine();

                bool continueMessaging = true;
                while (continueMessaging)
                {
                    Console.Write("Enter your message or default: ");
                    string requestMessage = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(requestMessage))
                    {
                        requestMessage = "Hello from client!";
                    }

                    serialPort.WriteLine(requestMessage);
                    Console.WriteLine("Request to Server: " + requestMessage);

                    string responseMessage = serialPort.ReadLine();
                    Console.WriteLine("Response from Server: " + responseMessage);

                    Console.Write("\nSend another? (y/n): ");
                    continueMessaging = Console.ReadLine()?.Trim().ToLower() == "y";

                    if (!continueMessaging)
                    {
                        Console.WriteLine("Exit.");
                    }
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }
    }

    static void DisplayAvailablePorts(string[] ports)
    {
        Console.WriteLine("Available COM ports:");
        for (int i = 0; i < ports.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {ports[i]}");
        }
    }

    static string SelectPort(string[] availablePorts)
    {
        Console.Write("\nSelect port number: ");
        if (int.TryParse(Console.ReadLine(), out int selection) &&
            selection >= 1 && selection <= availablePorts.Length)
        {
            return availablePorts[selection - 1];
        }
        Console.WriteLine("Invalid selection!");
        return SelectPort(availablePorts);
    }
}
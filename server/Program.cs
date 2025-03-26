using System;
using System.Data;
using System.Data.SqlTypes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LibData;

// ReceiveFrom();
class Program
{
    static void Main(string[] args)
    {
        ServerUDP.start();
    }
}

// StudentNumbers: 0864154, 0907615
// StudentNames: Elder dos Santos, Yasin Mesdar

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ServerUDP
{
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    static EndPoint clientEndpoint = new IPEndPoint(IPAddress.Parse(setting.ClientIPAddress), setting.ClientPortNumber);

    // TODO: [Read the JSON file and return the list of DNSRecords]

    public static void start()
    {
        byte[] buffer = new byte[1024];
        MessageService.clientEndpoint = clientEndpoint;

        Console.WriteLine("Server starting...");
        Console.WriteLine("");

        // TODO: [Create a socket and endpoints and bind it to the server IP address and port number]
        Socket ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint ServerEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        ServerSocket.Bind(ServerEndPoint);

        while (true)
        {
            // TODO:[Receive and print a received Message from the client] && [Receive and print Hello]
            Message receivedMessage = MessageService.receiveMessage(ServerSocket, buffer);
            Console.WriteLine($"[Received Message] type: {receivedMessage.MsgType}, content {receivedMessage.Content}");

            if (receivedMessage.MsgType == MessageType.Hello)
            {
                string content = "Hello from client";
                byte[] sendMessage = MessageService.serializeMessage(MessageType.Welcome, content);
                MessageService.sendMessage(ServerSocket, sendMessage, MessageType.Welcome, content);
            }
        }





        // TODO:[Send Welcome to the client]


        // TODO:[Receive and print DNSLookup]


        // TODO:[Query the DNSRecord in Json file]

        // TODO:[If found Send DNSLookupReply containing the DNSRecord]



        // TODO:[If not found Send Error]


        // TODO:[Receive Ack about correct DNSLookupReply from the client]


        // TODO:[If no further requests receieved send End to the client]

    }
}

public static class MessageService
{
    public static EndPoint clientEndpoint = null;

    public static byte[] serializeMessage(MessageType messageType, object content)
    {
        Message message = new();

        message.MsgId = 1001;
        message.MsgType = messageType;
        message.Content = content;

        byte[] serializedMessage = JsonSerializer.SerializeToUtf8Bytes(message);

        return serializedMessage;
    }

    public static void sendMessage(Socket ServerSocket, byte[] sendMessage, MessageType type, string content)
    {
        ServerSocket.SendTo(sendMessage, 0, sendMessage.Length, SocketFlags.None, clientEndpoint);
        Console.WriteLine($"[Sended message] Type: {type}, Content: {content}" );
    }

    public static Message extractMessage(byte[] data)
    {
        string receivedMessage = Encoding.ASCII.GetString(data);
        Message message = JsonSerializer.Deserialize<Message>(receivedMessage);

        return message;
    }

    public static Message receiveMessage(Socket ServerSocket, byte[] buffer)
    {
        int receivedBytes = ServerSocket.ReceiveFrom(buffer, ref clientEndpoint);
        string data = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
        Message message = JsonSerializer.Deserialize<Message>(data);

        return message;
    }
}
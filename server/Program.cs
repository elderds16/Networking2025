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
    static string DNSRecordsFile = File.ReadAllText("./DNSrecords.json");
    static List<DNSRecord> DNSRecords = JsonSerializer.Deserialize<List<DNSRecord>>(DNSRecordsFile);

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
            Console.WriteLine($"[Incoming] ← Type: {receivedMessage.MsgType}, Content: {receivedMessage.Content}");

            if (receivedMessage.MsgType == MessageType.Hello)
            {
                string content = "Welcome from server";
                byte[] sendMessage = MessageService.serializeMessage(1, MessageType.Welcome, content);
                MessageService.sendMessage(ServerSocket, sendMessage, MessageType.Welcome, content);
            }

            if (receivedMessage.MsgType == MessageType.DNSLookup)
            {
                try
                {
                    DNSRecord DNSrecord = JsonSerializer.Deserialize<DNSRecord>(receivedMessage.Content.ToString());

                    if(DNSRecords.Any(record => record.Type == DNSrecord.Type && record.Name == DNSrecord.Name))
                    {
                        DNSRecord existingRecord = DNSRecords.Find(record => record.Type == DNSrecord.Type && record.Name == DNSrecord.Name);
    
                        DNSrecord.Value = existingRecord.Value;
                        DNSrecord.TTL = existingRecord.TTL;
                        DNSrecord.Priority = existingRecord.Priority;

                        byte[] sendMessage = MessageService.serializeMessage(receivedMessage.MsgId, MessageType.DNSLookupReply, DNSrecord);
                        MessageService.sendDNSRecord(ServerSocket, sendMessage, MessageType.DNSLookupReply, DNSrecord);
                    }
                    else
                    {
                        byte[] sendMessage = MessageService.serializeMessage(receivedMessage.MsgId, MessageType.Error, DNSrecord);
                        MessageService.sendDNSRecord(ServerSocket, sendMessage, MessageType.DNSLookupReply, DNSrecord);
                    }
                }
                catch
                {
                    if (DNSRecords.Any(record => record.Name == receivedMessage.Content.ToString()))
                    {
                        DNSRecord existingRecord = DNSRecords.Find(record => record.Name == receivedMessage.Content.ToString());

                        byte[] sendMessage = MessageService.serializeMessage(receivedMessage.MsgId, MessageType.DNSLookupReply, existingRecord);
                        MessageService.sendDNSRecord(ServerSocket, sendMessage, MessageType.DNSLookupReply, existingRecord);
                    }
                    else
                    {
                        string content = "Domain not found";
                        byte[] sendMessage = MessageService.serializeMessage(receivedMessage.MsgId, MessageType.Error, content);
                        MessageService.sendMessage(ServerSocket, sendMessage, MessageType.Error, content);
                    }
                }
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

    public static byte[] serializeMessage(int id, MessageType messageType, object content)
    {
        Message message = new();

        message.MsgId = id;
        message.MsgType = messageType;
        message.Content = content;

        byte[] serializedMessage = JsonSerializer.SerializeToUtf8Bytes(message);

        return serializedMessage;
    }

    public static void sendMessage(Socket ServerSocket, byte[] sendMessage, MessageType type, string content)
    {
        ServerSocket.SendTo(sendMessage, 0, sendMessage.Length, SocketFlags.None, clientEndpoint);
        Console.WriteLine($"[Outgoing] → Type: {type}, Content: {content}" );
    }

    public static void sendDNSRecord(Socket ServerSocket, byte[] sendMessage, MessageType type, object content)
    {
        ServerSocket.SendTo(sendMessage, 0, sendMessage.Length, SocketFlags.None, clientEndpoint);
        Console.WriteLine($"[Outgoing] → Id: {1100}, Type: {type}, Content: {JsonSerializer.Serialize(content)}" );
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
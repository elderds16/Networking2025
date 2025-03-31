﻿using System;
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
        new ServerUDP().Start();
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

public class ServerUDP
{
    private static readonly string ConfigFile = "../Setting.json";
    private static readonly string DnsRecordsFile = "./DNSrecords.json";

    private Setting _setting;
    private List<DNSRecord> _dnsRecords;
    private Socket _serverSocket;
    private byte[] _buffer = new byte[1024];
    private EndPoint _clientEndpoint;
    private Message receivedMessage;

    public ServerUDP()
    {
        _setting = JsonSerializer.Deserialize<Setting>(File.ReadAllText(ConfigFile));
        _dnsRecords = JsonSerializer.Deserialize<List<DNSRecord>>(File.ReadAllText(DnsRecordsFile));
        _clientEndpoint = new IPEndPoint(IPAddress.Parse(_setting.ClientIPAddress), _setting.ClientPortNumber);
    }

    public void Start()
    {
        try
        {
            InitializeServer();
            while (true)
            {                
                try
                {
                    ReceiveHello();
                    SendWelcome();
                    HandleLookUps();
                    SendEnd();
                }
                catch (Exception ex)
                {
                    HandleError(ex);
                }
            }
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
        finally
        {
            _serverSocket.Dispose();
        }
    }

    private void InitializeServer()
    {
        try
        {
            MessageService.clientEndpoint = _clientEndpoint;
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(_setting.ServerIPAddress), _setting.ServerPortNumber);
            _serverSocket.Bind(serverEndPoint);
            Console.WriteLine("");
            MessageService.Logging("[Initializing] Server started");
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException("Failed to bind the server socket.", ex);
        }
    }


    private void ReceiveHello()
    {
        receivedMessage = MessageService.receiveMessage(_serverSocket, _buffer);

        if (receivedMessage.MsgType == MessageType.Hello)
        {
            MessageService.Logging($"[Incoming] ← MsgId: {receivedMessage.MsgId}, MsgType: Hello, Content: {receivedMessage.Content}");
        }
        else
        {
            throw new InvalidOperationException($"[Error] Expected Hello message but received {receivedMessage.MsgType} instead.");
        }
    }


    private void SendWelcome()
    {
        if (receivedMessage.MsgType != MessageType.Hello)
        {
            throw new InvalidOperationException($"[Error] Cannot send Welcome. Last message was not Hello, but {receivedMessage.MsgType}.");
        }

        string content = "Welcome from server";
        byte[] sendMessage = MessageService.serializeMessage(receivedMessage.MsgId, MessageType.Welcome, content);
        MessageService.sendMessage(_serverSocket, sendMessage, receivedMessage.MsgId, MessageType.Welcome, content);
    }


    private void HandleLookUps()
    {
        bool receiving = true;

        while (receiving)
        {
            receivedMessage = MessageService.receiveMessage(_serverSocket, _buffer);

            switch (receivedMessage.MsgType)
            {
                case MessageType.DNSLookup:
                    MessageService.Logging($"[Incoming] ← MsgId: {receivedMessage.MsgId}, MsgType: DNSLookup, Content: {JsonSerializer.Serialize(receivedMessage.Content)}");
                    HandleSingleLookup();
                    break;

                case MessageType.Ack:
                    MessageService.Logging($"[ACKnowledged] ← MsgId: {receivedMessage.MsgId}, MsgType: {receivedMessage.MsgType}, Content: {JsonSerializer.Serialize(receivedMessage.Content)}");
                    break;

                case MessageType.End:
                    MessageService.Logging($"[Incoming] ← MsgId: {receivedMessage.MsgId}, MsgType: {receivedMessage.MsgType}, Content: {JsonSerializer.Serialize(receivedMessage.Content)}");
                    receiving = false;
                    break;

                default:
                    MessageService.Logging($"[Error] Unexpected message type: {receivedMessage.MsgType}");
                    break;
            }
        }

    }

    private void HandleSingleLookup()
    {
        try
        {
            DNSRecord? dnsRecord = JsonSerializer.Deserialize<DNSRecord>(receivedMessage.Content?.ToString());

            if (dnsRecord == null || string.IsNullOrWhiteSpace(dnsRecord.Type) || string.IsNullOrWhiteSpace(dnsRecord.Name))
                throw new Exception("Invalid or incomplete DNS record request.");

            var match = _dnsRecords.Find(record => record.Type == dnsRecord.Type && record.Name == dnsRecord.Name);

            if (match != null)
            {
                dnsRecord.Value = match.Value;
                dnsRecord.TTL = match.TTL;
                dnsRecord.Priority = match.Priority;

                byte[] response = MessageService.serializeMessage(receivedMessage.MsgId, MessageType.DNSLookupReply, dnsRecord);
                MessageService.sendDNSRecord(_serverSocket, response, receivedMessage.MsgId, MessageType.DNSLookupReply, dnsRecord);
            }
            else
            {
                throw new Exception("DNS record not found.");
            }
        }
        catch (Exception ex)
        {
            string errorMsg = ex is JsonException
                ? $"Error in message with id {receivedMessage.MsgId}: Invalid DNSLookup format."
                : $"Error in message with id {receivedMessage.MsgId}: {ex.Message}";

            byte[] errorMessage = MessageService.serializeMessage(9999, MessageType.Error, errorMsg);
            MessageService.sendMessage(_serverSocket, errorMessage, 9999, MessageType.Error, errorMsg);
        }
    }

    private void SendEnd()
    {
        try
        {
            string content = "End Message. Client Closing. Server is still up and running!";
            byte[] sendMessage = MessageService.serializeMessage(8888, MessageType.End, content);
            MessageService.sendMessage(_serverSocket, sendMessage, 8888, MessageType.End, content);
            Console.WriteLine("");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[Error] Failed to send End message to client.", ex);
        }
        
    }


    private void HandleError(Exception ex)
    {
        if (ex is ClientErrorMessageException clientError)
        {
            MessageService.Logging($"[Error] Client Error → {clientError.Message}");
        }
        else
        {
            try
            {
                MessageService.Logging($"[Error] {ex.GetType().Name} → {ex.Message}");

                string errorMsg = $"[Error] {ex.Message}";
                byte[] errorMessage = MessageService.serializeMessage(9999, MessageType.Error, errorMsg);
                MessageService.sendMessage(_serverSocket, errorMessage, 9999, MessageType.Error, errorMsg);
            }
            catch
            {
                MessageService.Logging("[Error] → Failed to send error message to client.");
            }
        }
    }

}

public static class MessageService
{
    public static EndPoint clientEndpoint = null;

    public static byte[] serializeMessage(int id, MessageType messageType, object content)
    {
        Message message = new() { MsgId = id, MsgType = messageType, Content = content };
        return JsonSerializer.SerializeToUtf8Bytes(message);
    }

    public static void sendMessage(Socket serverSocket, byte[] sendMessage, int msgId, MessageType type, string content)
    {
        serverSocket.SendTo(sendMessage, 0, sendMessage.Length, SocketFlags.None, clientEndpoint);

        string label;

        switch (type)
        {
            case MessageType.Error:
                label = "[Error]";
                break;
            case MessageType.End:
                label = "[End]";
                break;
            default:
                label = "[Outgoing]";
                break;
        }

        Logging($"{label} → MsgId: {msgId}, MsgType: {type}, Content: {content}");
    }
    
    public static void sendDNSRecord(Socket serverSocket, byte[] sendMessage, int msgId, MessageType type, object content)
    {
        serverSocket.SendTo(sendMessage, 0, sendMessage.Length, SocketFlags.None, clientEndpoint);
        Logging($"[Outgoing] → MsgId: {msgId}, MsgType: {type}, Content: {JsonSerializer.Serialize(content)}");
    }

    public static Message receiveMessage(Socket serverSocket, byte[] buffer)
    {
        try
        {
            int receivedBytes = serverSocket.ReceiveFrom(buffer, ref clientEndpoint);
            string data = Encoding.UTF8.GetString(buffer, 0, receivedBytes);

            Message? message = JsonSerializer.Deserialize<Message>(data);

            if (message == null)
            {
                throw new Exception("Deserialization returned null. Invalid message format.");
            }

            return message;
        }
        catch (SocketException ex)
        {
            throw new Exception("Socket error while receiving message: " + ex.Message);
        }
        catch (JsonException ex)
        {
            throw new Exception("Invalid JSON format received: " + ex.Message);
        }
    }


    public static void Logging(string message)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    }

}

public class ClientErrorMessageException : Exception
{
    public ClientErrorMessageException(string message) : base(message)
    {
    }
}

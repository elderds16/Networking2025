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

    public void start()
    {
        try
        {
            InitializeServer();
            while (true)
            {
                ResetState();

                try
                {
                    ReceiveHello();
                    SendWelcome();
                    HandleLookUps()
                ;   SendEnd();
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
            _udpSocket.Dispose();
        }
    }



    private void InitializeServer()
    {
        try
        {
            byte[] buffer = new byte[1024];
            MessageService.clientEndpoint = clientEndpoint;                       

            // TODO: [Create a socket and endpoints and bind it to the server IP address and port number]
            Socket ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ServerEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
            ServerSocket.Bind(ServerEndPoint);

            MessageService.Logging("[Initalizing] Server starting...");
            Console.WriteLine("");

            //var serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerIpAddress), ServerPort);
            //_udpSocket.Bind(serverEndPoint);
            //_udpSocket.ReceiveTimeout = 0;

            //Log($"Server: Listening on {ServerIpAddress}:{ServerPort}...");
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException($"Failed to bind the server to {setting.ServerIpAddress}:{settingServerPortNumber}. Please ensure the port is not in use and the IP address is valid.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An unexpected error occurred while initializing the server.", ex);
        }

        private void ResetState()
        {
            _clientEndPoint = null;
            
        }

    private void ReceiveHello()
    {
        try
        {
            Message receivedMessage = MessageService.receiveMessage(ServerSocket, buffer);
            if (receivedMessage.MsgType == MessageType.Hello)
            {
                MessageService.Logging($"[Incoming] ← MsgId: {receivedMessage.MsgId}, MsgType: {receivedMessage.MsgType}, Content: {JsonSerializer.Serialize(receivedMessage.Content)}");
            }
            else
            {
                throw new InvalidOperationException("The server received an unexpected message from the client.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An unexpected error occurred while receiving the Hello message.", ex);
        }
    }

    private void SendWelcome()
    {
        if (receivedMessage.MsgType == MessageType.Hello)
        {
            try
            {
                string content = "Welcome from server";
                byte[] sendMessage = MessageService.serializeMessage(receivedMessage.MsgId, MessageType.Welcome, content);
                MessageService.sendMessage(ServerSocket, sendMessage, receivedMessage.MsgId, MessageType.Welcome, content);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An unexpected error occurred while sending the Welcome message.", ex);
            }
        }
    }

    private void HandleLookUps()
    {
        Message receivedMessage = MessageService.receiveMessage(ServerSocket, buffer);
        if (receivedMessage.MsgType == MessageType.DNSLookup)
        {

            try
            {
                // Probeer DNSRecord te deserializen
                var dnsContent = receivedMessage.Content?.ToString();
                DNSRecord? dnsRecord = JsonSerializer.Deserialize<DNSRecord>(dnsContent);

                // Controleer of DNSRecord geldig is
                if (dnsRecord == null)
                    throw new Exception("Invalid DNSRecord format.");

                if (string.IsNullOrWhiteSpace(dnsRecord.Type) && string.IsNullOrWhiteSpace(dnsRecord.Name))
                    throw new Exception("Both fields 'Type' and 'Name'cannot be empty.");

                if (string.IsNullOrWhiteSpace(dnsRecord.Type))
                    throw new Exception("Field 'Type' cannot be empty.");

                if (string.IsNullOrWhiteSpace(dnsRecord.Name))
                    throw new Exception("Field 'Name' cannot be empty.");

                // Check of het record bestaat in de lijst
                var match = DNSRecords.Find(record => record.Type == dnsRecord.Type && record.Name == dnsRecord.Name);

                if (match != null)
                {
                    dnsRecord.Value = match.Value;
                    dnsRecord.TTL = match.TTL;
                    dnsRecord.Priority = match.Priority;

                    byte[] sendMessage = MessageService.serializeMessage(receivedMessage.MsgId, MessageType.DNSLookupReply, dnsRecord);
                    MessageService.sendDNSRecord(ServerSocket, sendMessage, receivedMessage.MsgId, MessageType.DNSLookupReply, dnsRecord);
                }
                else
                {
                    throw new Exception("DNS record not found.");
                }
            }
            catch (Exception ex)
            {
                string errorMsg;

                if (ex is JsonException)
                {
                    errorMsg = $"Error in message with id {receivedMessage.MsgId}: Invalid DNSLookup format.";
                }
                else
                {
                    errorMsg = $"Error in message with id {receivedMessage.MsgId}: {ex.Message}";
                }

                byte[] errorMessage = MessageService.serializeMessage(9999, MessageType.Error, errorMsg);
                MessageService.sendMessage(ServerSocket, errorMessage, 9999, MessageType.Error, errorMsg);
            }

        }
    }

    private void SendError()
    {

        //if (_clientEndPoint is IPEndPoint endPoint &&
        //    endPoint.Address.Equals(IPAddress.Any) &&
        //    endPoint.Port == 0)
        //{
        //    return;
        //}

        //var message = new Message { Type = MessageType.Error, Content = errorMessage };
        //SendMessage(message);
        //Log($"Server: Sent -> type: Error, content: {errorMessage}");
    }

    private void HandleError(Exception ex)
    {
        if (ex is ClientErrorMessageException clientError)
        {
            Log($"Server: Client Error -> {clientError.Message}");
        }
        else
        {
            try
            {
                Log($"Server: Error {ex.GetType().Name} -> {ex.Message}");
                SendError(ex.Message);
            }
            catch
            {
                Log("Failed to send error message to Client.");
            }
        }
    }

    private void SendEnd()
    {
        Message receivedMessage = MessageService.receiveMessage(ServerSocket, buffer);

        try
        {
            string content = "End Message. Client Closing";
            byte[] sendMessage = MessageService.serializeMessage(8888, MessageType.End, content);
            MessageService.sendMessage(ServerSocket, sendMessage, 8888, MessageType.End, content);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An unexpected error occurred while sending the End Message.", ex);
        }
    }

    private void ack()
    {
        if (receivedMessage.MsgType == MessageType.Ack)
        {
            MessageService.Logging($"[ACKnowledged] ← MsgId: {receivedMessage.MsgId}, MsgType: {receivedMessage.MsgType}, Content: {JsonSerializer.Serialize(receivedMessage.Content)}");
        }
        else
        {
            MessageService.Logging($"[Incoming] ← MsgId: {receivedMessage.MsgId}, MsgType: {receivedMessage.MsgType}, Content: {JsonSerializer.Serialize(receivedMessage.Content)}");
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

        public static void sendMessage(Socket ServerSocket, byte[] sendMessage, int msgId, MessageType type, string content)
        {
            ServerSocket.SendTo(sendMessage, 0, sendMessage.Length, SocketFlags.None, clientEndpoint);
            Logging($"[Outgoing] → MsgId: {msgId}, MsgType: {type}, Content: {content}");
        }

        public static void sendDNSRecord(Socket ServerSocket, byte[] sendMessage, int msgId, MessageType type, object content)
        {
            ServerSocket.SendTo(sendMessage, 0, sendMessage.Length, SocketFlags.None, clientEndpoint);
            Logging($"[Outgoing] → MsgId: {msgId}, MsgType: {type}, Content: {JsonSerializer.Serialize(content)}");

        }

        public static Message extractMessage(byte[] data)
        {
            string receivedMessage = Encoding.ASCII.GetString(data);
            Message message = JsonSerializer.Deserialize<Message>(receivedMessage);

            return message;
        }

        public static Message receiveMessage(Socket ServerSocket, byte[] buffer)
        {
            
            try 
            {
                int receivedBytes = ServerSocket.ReceiveFrom(buffer, ref clientEndpoint);
                string data = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                Message message = JsonSerializer.Deserialize<Message>(data);

                if (message.Type == MessageType.Error)
                {
                    throw new ClientErrorMessageException($"Received Error message: {message.Content}");
                }

                return message;

            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                throw new InvalidOperationException("Socket threw an exception because earlier a message was sent to the client after the client already closed the socket");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                throw new TimeoutException("Did not receive a message from the client in a timely manner.");
            }
            catch (Exception ex) when (ex is not ClientErrorMessageException)
            {
                throw new InvalidOperationException("An unexpected error occurred while trying to receive a message from the client.", ex);
            }
        }

        

        public static void Logging(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Message is empty!");
            }
        }













    }












}
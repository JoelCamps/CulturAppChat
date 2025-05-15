using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CulturAppChat.Models;

namespace CulturAppChat
{
    class Program
    {
        private static CulturAppEntities db = new CulturAppEntities();
        private static readonly List<TcpClient> clients = new List<TcpClient>();
        private static readonly object sync = new object();

        static void Main(string[] args)
        {
            int port = 6400;
            Console.WriteLine($"Iniciando servidor en puerto {port}...");
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine("Esperando conexiones...");
            while (true)
            {
                var client = listener.AcceptTcpClient();
                lock (sync) { clients.Add(client); }
                Console.WriteLine("Cliente conectado.");
                Task.Run(() => HandleClientAsync(client));
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                {
                    var history = db.Menssages
                            .OrderBy(m => m.send_datetime)
                            .ToList();
                    foreach (var m in history)
                    {
                        var userName = db.Users.Find(m.user_id)?.name ?? $"Usuario {m.user_id}";
                        var userSurname = db.Users.Find(m.user_id)?.surname ?? $"Usuario {m.user_id}";
                        var formatted = $"{userName + " " + userSurname}: {m.message}  {m.send_datetime:yyyy-MM-dd HH:mm}";
                        await writer.WriteLineAsync(formatted);
                    }

                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        int userId = 0;
                        int.TryParse(parts[0], out userId);
                        var text = parts.Length > 1 ? parts[1] : string.Empty;
                        var now = DateTime.UtcNow;

                        var mensaje = new Menssages
                        {
                            user_id = userId,
                            message = text,
                            send_datetime = now
                        };

                        db.Menssages.Add(mensaje);
                        await db.SaveChangesAsync();

                        var usuario = db.Users.Find(userId)?.name ?? $"Usuario {userId}";
                        var formatted = $"{usuario}: {text}  {now:yyyy-MM-dd HH:mm}";
                        BroadcastMessage(formatted);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en cliente: {ex.Message}");
            }
            finally
            {
                lock (sync) { clients.Remove(client); }
                client.Close();
                Console.WriteLine("Cliente desconectado.");
            }
        }

        private static void BroadcastMessage(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            lock (sync)
            {
                foreach (var client in clients.ToList())
                {
                    try
                    {
                        var stream = client.GetStream();
                        if (stream.CanWrite)
                        {
                            stream.Write(data, 0, data.Length);
                        }
                    }
                    catch
                    {
                        clients.Remove(client);
                    }
                }
            }
        }
    }
}

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
        // Instancia de la base de datos (Entity Framework)
        private static CulturAppEntities db = new CulturAppEntities();

        // Lista para almacenar todos los clientes conectados
        private static readonly List<TcpClient> clients = new List<TcpClient>();

        // Objeto para sincronización de acceso a la lista de clientes
        private static readonly object sync = new object();

        /// <summary>
        /// Método principal que inicia el servidor TCP y acepta conexiones entrantes.
        /// </summary>
        static void Main(string[] args)
        {
            int port = 6400;
            Console.WriteLine($"Iniciando servidor en puerto {port}...");

            // Crea un listener en todas las interfaces de red (0.0.0.0) y en el puerto especificado
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine("Esperando conexiones...");

            // Bucle infinito para aceptar múltiples clientes
            while (true)
            {
                var client = listener.AcceptTcpClient();

                // Añade el cliente a la lista de forma segura
                lock (sync) { clients.Add(client); }
                Console.WriteLine("Cliente conectado.");

                // Maneja la conexión del cliente en un hilo separado
                Task.Run(() => HandleClientAsync(client));
            }
        }

        /// <summary>
        /// Maneja la comunicación con un cliente de forma asíncrona.
        /// Recibe y guarda mensajes, y los retransmite a todos los clientes.
        /// </summary>
        private static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
                {
                    // Recupera el historial de mensajes desde la base de datos y los envía al cliente
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
                    // Lee mensajes entrantes del cliente
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        int userId = 0;
                        int.TryParse(parts[0], out userId);  // Extrae el ID del usuario
                        var text = parts.Length > 1 ? parts[1] : string.Empty;
                        var now = DateTime.UtcNow;

                        // Crea un nuevo objeto de mensaje y lo guarda en la base de datos
                        var mensaje = new Menssages
                        {
                            user_id = userId,
                            message = text,
                            send_datetime = now
                        };

                        db.Menssages.Add(mensaje);
                        await db.SaveChangesAsync();

                        var userName = db.Users.Find(userId)?.name ?? $"Usuario {userId}";
                        var userSurname = db.Users.Find(userId)?.surname ?? $"Usuario {userId}";
                        var formatted = $"{userName + " " + userSurname}: {text}  {now:yyyy-MM-dd HH:mm}";

                        // Envía el mensaje a todos los clientes conectados
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

        /// <summary>
        /// Envía un mensaje a todos los clientes actualmente conectados.
        /// </summary>
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
using System;
using MphRead.Entities;

namespace MphRead.Mods.Network
{
    /// <summary>
    /// `MphRead -connect &lt;host&gt; [-port N] [-name NAME] [-hunter H]`
    ///
    /// The same session the Windows launcher starts, without WinForms. It
    /// exists for two reasons that turned out to be the same reason: it is
    /// the only way to run a client on Linux or from a script, and it is what
    /// lets two clients be started side by side and compared. Every bug in
    /// this feature so far has been one that only appears with two real
    /// clients in the same match, which is precisely what a launcher dialog
    /// makes awkward to arrange.
    /// </summary>
    public static class NetConnectCommand
    {
        public static void Run(string host, int port, string playerName, Hunter hunter, int recolor)
        {
            if (!NetLaunch.Join(host, port, playerName, hunter, color: recolor))
            {
                Console.WriteLine("[net] could not join; giving up");
                NetSession.Stop();
                return;
            }
            try
            {
                (string RoomKey, GameMode Mode) room = NetLaunch.ServerRoom()!.Value;
                using var renderer = new RenderWindow();
                NetLaunch.BuildPlayers(renderer.Scene, hunter, recolor);
                renderer.AddRoom(room.RoomKey, room.Mode, playerCount: NetLaunch.RoomPlayerCount);
                Console.WriteLine($"[net] loading {room.RoomKey} ({room.Mode})");
                renderer.Run();
            }
            finally
            {
                // The session owns a worker thread and a bound socket; a crash
                // in the game must not leave either behind.
                NetSession.Stop();
                NetLog.Close();
            }
        }
    }
}

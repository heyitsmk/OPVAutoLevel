using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Threading;
using Eleon;
using Eleon.Modding;
using YamlDotNet.Serialization;

namespace OPVAutoLevel
{
    public class OPVAutoLevel : IMod
    { 
        private readonly Dictionary<string, PlayfieldManager> _playfields = new Dictionary<string, PlayfieldManager>();

        internal BlockService BlockService { get; private set; } = null!;
        internal IModApi Api { get; private set; } = null!;

        public void Init(IModApi modAPI)
        {            
            Api = modAPI;
            Api.Log("OPVAutoLevelMod - Initializing");
            Api.Log("OPVAutoLevelMod - Parsing Block Configs");
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = Path.GetDirectoryName(assemblyPath);
                var yamlContent = File.ReadAllText(assemblyDirectory + "\\OPVAutoLevel_Info.yaml");
                var deserializer = new DeserializerBuilder()
                    .Build();
                var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
                var configPath = ((string)yamlDict["ConfigPath"]).TrimEnd('\\') + "\\BlocksConfig.ecf";
                Api.Log($"Config Path: {configPath}");
                BlockService = new BlockService(configPath);
            }
            catch (Exception ex)
            {
                Api.Log("OPVAutoLevelMod - Error loading block config");
                Api.Log("OPVAutoLevelMod - " + ex.ToString());
                return;
            }
            Api.Application.ChatMessageSent += Application_ChatMessageSent;
            Api.Application.OnPlayfieldLoaded += Application_OnPlayfieldLoaded;
            Api.Application.OnPlayfieldUnloading += Application_OnPlayfieldUnloading;
            Api.Application.Update += Application_Update;
        }

        internal void OnEntityDisabled(IPlayfield pf, IEntity e)
        {
            foreach (var player in pf.Players)
            {
                Api.Application.SendChatMessage(new MessageData()
                {
                    RecipientEntityId = player.Key,
                    Text = $"{e.Name} has been disabled and will be auto-rotated in 15 seconds.",
                    Channel = Eleon.MsgChannel.SinglePlayer,
                    SenderType = Eleon.SenderType.ServerPrio
                });
            }
            new Thread(() =>
            {
                Thread.Sleep(15000);
                e.MoveStop();
                float zAngle = e.Rotation.eulerAngles.z;                
                e.Rotation = UnityEngine.Quaternion.Euler(0, 0, zAngle);
            }).Start();
        }

        internal void MessageAllPlayers(IPlayfield pf, string message)
        {
            foreach (var player in pf.Players)
            {
                Api.Application.SendChatMessage(new MessageData()
                {
                    RecipientEntityId = player.Key,
                    Text = message,
                    Channel = Eleon.MsgChannel.SinglePlayer,
                    SenderType = Eleon.SenderType.ServerPrio
                });
            }
        }

        private void Application_ChatMessageSent(MessageData chatMsgData)
        {
            if (chatMsgData.Text.ToLower().Contains("!mods") && chatMsgData.Channel == Eleon.MsgChannel.Server)
            {
                Api.Application.SendChatMessage(new MessageData()
                {
                    RecipientEntityId = chatMsgData.SenderEntityId,
                    Text = "OPVAutoLevelMod v0.1",
                    Channel = Eleon.MsgChannel.SinglePlayer,
                    SenderType = Eleon.SenderType.System
                });
            }
        }        

        private void Application_OnPlayfieldUnloading(IPlayfield playfield)
        {
            try
            {
                if (_playfields.ContainsKey(playfield.Name))
                {
                    Api.Log($"OPVAutoLevel - Unloading playfield {playfield.Name}");
                    var pfm = _playfields[playfield.Name];
                    pfm.Unload();
                    _playfields.Remove(playfield.Name);
                }
            }
            catch (Exception ex)
            {
                Api.LogError(ex.Message);
            }
        }

        private void Application_OnPlayfieldLoaded(IPlayfield playfield)
        {
            try
            { 
                if (!_playfields.ContainsKey(playfield.Name))
                {
                    Api.Log($"OPVAutoLevel - Loading playfield {playfield.Name}");
                    var pfm = new PlayfieldManager(this, playfield);                
                    _playfields.Add(playfield.Name, pfm);
                    pfm.Load();
                }   
            }
            catch (Exception ex)
            {
                Api.LogError(ex.Message);
            }
        }

        private void Application_Update()
        {
            try
            {
                foreach (var kvp in _playfields)
                {
                    kvp.Value.Update();
                }
            }
            catch (Exception ex)
            {
                Api.LogError(ex.Message);
            }
        }

        public void Shutdown()
        {
            Api.Log("OPVAutoLevelMod - Shutting Down");
        }
    }
}

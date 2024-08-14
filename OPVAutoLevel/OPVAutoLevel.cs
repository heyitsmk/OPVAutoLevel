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
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
using System.Threading.Tasks;

namespace OPVAutoLevel
{
    public class OPVAutoLevel : IMod
    { 
        private readonly Dictionary<string, PlayfieldManager> _playfields = new Dictionary<string, PlayfieldManager>();
        private bool _loaded = false;
        private string _version = "";

        internal int ActivationDelay { get; private set; } = 15;
        internal IModApi Api { get; private set; } = null!;
        internal BlockService BlockService { get; private set; } = null!;
        internal bool EnableCores { get; private set; }
        internal bool EnableGenerators { get; private set; }
        internal bool EnableThrusters { get; private set; }    
        
        public void Init(IModApi modAPI)
        {            
            Api = modAPI;
            Api.Log("OPVAutoLevelMod - Initializing");
            Api.Log("OPVAutoLevelMod - Parsing Block Configs");
            _loaded = LoadConfig();
            if (_loaded)
            {
                Api.Application.OnPlayfieldLoaded += Application_OnPlayfieldLoaded;
                Api.Application.OnPlayfieldUnloading += Application_OnPlayfieldUnloading;
                Api.Application.Update += Application_Update;
            }
            Api.Application.ChatMessageSent += Application_ChatMessageSent;            
        }

        internal void OnEntityDisabled(IPlayfield pf, IEntity e)
        {
            foreach (var player in pf.Players)
            {
                Api.Application.SendChatMessage(new MessageData()
                {
                    RecipientEntityId = player.Key,
                    Text = $"{e.Name} has been disabled and will be auto-rotated in {ActivationDelay} seconds.",
                    Channel = Eleon.MsgChannel.SinglePlayer,
                    SenderType = Eleon.SenderType.ServerPrio
                });
            }
            Task.Run(async () =>
            {
                await Task.Delay(ActivationDelay * 1000);
                foreach (var player in pf.Players)
                {
                    Api.Application.SendChatMessage(new MessageData()
                    {
                        RecipientEntityId = player.Key,
                        Text = $"Beginning auto-levelling of {e.Name} - please wait 5 seconds",
                        Channel = Eleon.MsgChannel.SinglePlayer,
                        SenderType = Eleon.SenderType.ServerPrio
                    });
                }
                e.MoveStop();
                await Task.Delay(5000);
                AutoLevel(e);
                foreach (var player in pf.Players)
                {
                    Api.Application.SendChatMessage(new MessageData()
                    {
                        RecipientEntityId = player.Key,
                        Text = $"Auto-levelling of {e.Name} complete.",
                        Channel = Eleon.MsgChannel.SinglePlayer,
                        SenderType = Eleon.SenderType.ServerPrio
                    });
                }
            });
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
                    Text = $"OPVAutoLevelMod v{_version} - Cores: {EnableCores} Thrusters: {EnableThrusters} Generators: {EnableGenerators} Delay: {ActivationDelay}",
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

        private bool LoadConfig()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = Path.GetDirectoryName(assemblyPath);
                var yamlContent = File.ReadAllText(assemblyDirectory + "\\OPVAutoLevel_Info.yaml");
                var deserializer = new DeserializerBuilder().Build();
                var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
                var configPath = ((string)yamlDict["ConfigPath"]).TrimEnd('\\') + "\\BlocksConfig.ecf";
                Api.Log($"Config Path: {configPath}");
                BlockService = new BlockService(configPath);
                if (yamlDict.TryGetValue("EnableCores", out object value))
                    EnableCores = bool.Parse((string)value);
                if (yamlDict.TryGetValue("EnableGenerators", out value))
                    EnableGenerators = bool.Parse((string)value);
                if (yamlDict.TryGetValue("EnableThrusters", out value))
                    EnableThrusters = bool.Parse((string)value);
                if (yamlDict.TryGetValue("Version", out value))
                    _version = (string)value;
                if (yamlDict.TryGetValue("ActivationDelay", out value))
                    ActivationDelay = int.Parse((string)value);
            }
            catch (Exception ex)
            {
                Api.Log($"OPVAutoLevel - Load Config - {ex.Message}");
            }
            return true;
        }

        private void AutoLevel(IEntity e)
        {
            var forward = e.Rotation * Vector3.forward;
            var forwardXZ = new Vector3(forward.x, 0, forward.z);
            forwardXZ.Normalize();
            var desiredRotation = Quaternion.LookRotation(forwardXZ, Vector3.up);
            var finalRotation = Quaternion.FromToRotation(Vector3.forward, forwardXZ);
            var alignedRotation = Quaternion.Euler(0, finalRotation.eulerAngles.y, 0);
            e.Rotation = alignedRotation;
        }


        public void Shutdown()
        {
            Api.Log("OPVAutoLevelMod - Shutting Down");
        }
    }
}

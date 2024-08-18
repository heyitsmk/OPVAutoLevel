using Eleon.Modding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OPVAutoLevel
{
    internal class PlayfieldManager
    {
        private readonly Dictionary<int, string> _blockIdToName = new Dictionary<int, string>();
        private readonly HashSet<int> _disabledEntities = new HashSet<int>();
        private readonly HashSet<int> _ignoredEntities = new HashSet<int>();
        private readonly OPVAutoLevel _mod;  
        private readonly IPlayfield _playfield;
        private readonly Dictionary<int, IEntity> _trackedEntities = new Dictionary<int, IEntity>();
        private long _lastUpdate = DateTime.Now.Ticks;

        internal PlayfieldManager(OPVAutoLevel mod, IPlayfield playfield)
        {
            _mod = mod;
            _playfield = playfield;
        }

        internal void Load()
        {
            _mod.Api.Log($"OPVAutoLevel - Loading {_playfield.Name}");            
            _playfield.OnEntityLoaded += OnEntityLoaded;
            _playfield.OnEntityUnloaded += OnEntityUnloaded;
            LoadBlockMap();
            CheckEntities();
        }

        internal void Unload()
        {
            _trackedEntities.Clear();
        }

        internal void Update()
        {
            if (_lastUpdate + TimeSpan.FromSeconds(1).Ticks < DateTime.Now.Ticks)
            {
                CheckEntities();
                foreach (var kvp in _trackedEntities)
                {
                    var e = kvp.Value;
                    e.LoadFromDSL();
                    if (IsDisabled(e))
                    {
                        _disabledEntities.Add(kvp.Key);
                        _mod.OnEntityDisabled(_playfield, e);
                    }
                }
                foreach (var eid in _disabledEntities)
                {
                    _trackedEntities.Remove(eid);
                }
                _lastUpdate = DateTime.Now.Ticks;
            }
        }

        private void CheckEntities()
        {
            foreach (var kvp in _playfield.Entities)
            {
                var id = kvp.Key;
                var e = kvp.Value;
                if (_trackedEntities.ContainsKey(id) || _ignoredEntities.Contains(id))
                    continue;
                if (IsValidEntity(e))
                {
                    e.LoadFromDSL();
                    if (IsValidStructure(e))
                    {
                        _mod.Api.Log($"OPVAutoLevel - Check Entities - Tracking entity {e.Name}:{e.Type}:{e.Faction.Group}:{e.Structure.CoreType}:{e.Id}");
                        _trackedEntities.Add(id, e);
                    }
                    else if (e.Type != EntityType.Proxy && (e.Structure?.CoreType ?? CoreType.NoData) != CoreType.NoData)
                    {
                        _mod.Api.Log($"OPVAutoLevel - Check Entities - Ignoring entity {e.Name}:{e.Type}:{e.Faction.Group}:{e.Structure?.CoreType ?? CoreType.NoData}:{e.Id}");
                        _ignoredEntities.Add(id);
                    }
                }
            }
        }

        private HashSet<string> EnumerateBlocks(IEntity e)
        {
            HashSet<string> blocks = new HashSet<string>();
            for (int x = e.Structure.MinPos.x; x <= e.Structure.MaxPos.x; x++)
            {
                for (int y = e.Structure.MinPos.y + 128; y <= e.Structure.MaxPos.y + 128; y++)
                {
                    for (int z = e.Structure.MinPos.z; z <= e.Structure.MaxPos.z; z++) 
                    { 
                        var b = e.Structure.GetBlock(x, y, z);
                        if (b != null) 
                        {
                            b.Get(out int type, out int _, out int _, out bool _);
                            if (type != 0 && _blockIdToName.ContainsKey(type) && _mod.BlockService.BlocksByName.ContainsKey(_blockIdToName[type]))
                            { 
                                blocks.Add(_mod.BlockService.BlocksByName[_blockIdToName[type]].Name);
                            }
                        }                            
                    }
                }
            }
            return blocks;
        }

        private bool IsDisabled(IEntity e)
        {            
            if (_mod.EnableCores && e.Structure.CoreType == CoreType.None)
            {
                _mod.Api.Log($"OPVAutoLevel - Disabled Check - Core removed from {e.Name}:{e.Id} - marking as disabled");
                return true;
            }
            if (_mod.EnableGenerators && !e.Structure.IsPowered)
            {
                _mod.Api.Log($"OPVAutoLevel - Disabled Check - {e.Name}:{e.Id} is no longer powered - marking as disabled");
                return true;
            }
            var blocks = EnumerateBlocks(e);
            if (_mod.EnableThrusters && blocks.All(b => !_mod.BlockService.Thrusters.ContainsKey(b)))
            {
                _mod.Api.Log($"OPVAutoLevel - Disabled Check - {e.Name}:{e.Id} has no thrusters - marking as disabled");
                return true;
            }
            if (_mod.EnableGenerators && blocks.All(b => !_mod.BlockService.Thrusters.ContainsKey(b)))
            {
                _mod.Api.Log($"OPVAutoLevel - Disabled Check - {e.Name}:{e.Id} has no generators - marking as disabled");
                return true;
            }
            return false;
        }

        private bool IsValidEntity(IEntity e)
        {
            return e.Type == EntityType.CV && e.Faction.Group != FactionGroup.Player && e.Faction.Group != FactionGroup.Admin;
        }

        private bool IsValidStructure(IEntity e)
        {   
            var coreCorrect = e.Structure.CoreType == CoreType.NPC || e.Structure.CoreType == CoreType.NoFaction;
            if (!coreCorrect)
            {
                _mod.Api.Log($"OPVAutoLevel - Structure Check - {e.Name}:{e.Id} - Invalid Core Type");
                return false;
            }
            if (!e.Structure.IsPowered)
            {
                _mod.Api.Log($"OPVAutoLevel - Structure Check - {e.Name}:{e.Id} - Is Not Powered");
                return false;
            }
            var blocks = EnumerateBlocks(e);
            var hasThrusters = blocks.Any(b => _mod.BlockService.Thrusters.ContainsKey(b));
            if (!hasThrusters)
            {
                _mod.Api.Log($"OPVAutoLevel - Structure Check - {e.Name}:{e.Id} - No Thrusters");
                return false;
            }
            var hasGenerators = blocks.Any(b => _mod.BlockService.Generators.ContainsKey(b));
            if (!hasGenerators)
            {
                _mod.Api.Log($"OPVAutoLevel - Structure Check - {e.Name}:{e.Id} - No Generators");
                return false;
            }
            return coreCorrect
                && e.Structure.IsPowered
                && hasThrusters
                && hasGenerators;
        }

        private void LoadBlockMap()
        {
            var map = _mod.Api.Application.GetBlockAndItemMapping();
            foreach (var item in map)
            {
                if (!_blockIdToName.ContainsKey(item.Value))
                    _blockIdToName.Add(item.Value, item.Key);
            }
        }

        private void OnEntityLoaded(IEntity e)
        {
            if (_trackedEntities.ContainsKey(e.Id)) return;
            if (e.Type == EntityType.CV && e.Faction.Group != FactionGroup.Player && e.Faction.Group != FactionGroup.Admin)
            {
                e.LoadFromDSL();
                if (e.Structure.CoreType == CoreType.NPC || e.Structure.CoreType == CoreType.NoFaction)
                {
                    _mod.Api.Log($"OPVAutoLevel - Entity Loaded - Tracking entity {e.Name}:{e.Type}:{e.Faction.Group}:{e.Structure.CoreType}:{e.Id}");
                    _trackedEntities.Add(e.Id, e);
                }
            }
        }

        private void OnEntityUnloaded(IEntity e)
        {
            if (_trackedEntities.ContainsKey(e.Id))
            {
                _mod.Api.Log($"OPVAutoLevel - Entity Unloaded - Unloading tracked entity {e.Name}:{e.Id}");
                _trackedEntities.Remove(e.Id);
            }
        }        
    }
}

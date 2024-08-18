using EgsLib.ConfigFiles.Ecf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OPVAutoLevel
{
    public class Block : EgsLib.ConfigFiles.Block
    {
        private readonly BlockService _blockService;

        public Block(BlockService blockService, IEcfObject obj)
            : base(obj)
        {
            _blockService = blockService;
        }

        public IEnumerable<string> GetClasses()
        {
            if (!string.IsNullOrEmpty(Class)) return new List<string>() { Class };
            if (!string.IsNullOrEmpty(Reference)) return GetReference()?.GetClasses() ?? new List<string>() { "Unclassified" };
            if (ParentBlocks.Count > 0) return GetParents().SelectMany(b => b.GetClasses() ?? new List<string>() { "Unclassified" }).Distinct();
            if (!string.IsNullOrEmpty(TemplateRoot)) return GetTemplateRoot()?.GetClasses() ?? new List<string>() { "Unclassified" };
            return new List<string>() { "Unclassified" };
        }

        public IEnumerable<Block> GetChildren()
        {
            foreach (var blockName in ChildBlocks)
            {
                if (_blockService.BlocksByName.TryGetValue(blockName, out Block? block))
                    yield return block;
            }
        }

        public IEnumerable<Block> GetParents()
        {
            foreach (var blockName in ParentBlocks)
            {
                if (_blockService.BlocksByName.TryGetValue(blockName, out Block? block))
                    yield return block;
            }
        }

        public Block? GetReference()
        {
            if (_blockService.BlocksByName.TryGetValue(Reference, out Block? block))
                return block;
            return null;
        }

        public Block? GetTemplateRoot()
        {
            if (Name != TemplateRoot && _blockService.BlocksByName.TryGetValue(TemplateRoot, out Block? block))
                return block;
            return null;
        }
    }
}

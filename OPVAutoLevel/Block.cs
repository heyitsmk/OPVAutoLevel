using EgsLib.ConfigFiles.Ecf;
using System;
using System.Collections.Generic;
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

        public string GetClass()
        {
            var parent = GetParent();
            if (string.IsNullOrEmpty(Class) && parent != null)
                return parent.GetClass();
            return Class;
        }

        public IEnumerable<Block> GetChildren()
        {
            foreach (var blockName in ChildBlocks)
            {
                if (_blockService.BlockLookup.TryGetValue(blockName, out Block? value))
                {
                    yield return value;
                }
            }
        }

        public Block? GetParent()
        {
            if (ParentBlocks.Count > 0)
                return _blockService.BlockLookup[ParentBlocks[0]];
            return null;
        }
    }
}

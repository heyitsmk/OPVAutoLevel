using EgsLib.ConfigFiles;
using EgsLib.ConfigFiles.Ecf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace OPVAutoLevel
{
    public class BlockService
    {
        public ReadOnlyDictionary<string, Block> BlockLookup { get; private set; }
        public ReadOnlyCollection<Block> Blocks { get; private set; }
        public ReadOnlyDictionary<string, Block> Generators { get; private set; }
        public ReadOnlyDictionary<string, Block> Thrusters { get; private set; }

        public BlockService(string configPath)
        {
            var blockConfig = new EcfFile(configPath);
            var blockObjects = blockConfig.ParseObjects();            
            var blockNameLookup = new Dictionary<string, Block>();
            var generators = new Dictionary<string, Block>();
            var thrusters = new Dictionary<string, Block>();
            var blockList = new List<Block>();
            foreach (var blockObject in blockObjects)
            {
                var block = new Block(this, blockObject);
                blockNameLookup.Add(block.Name, block);
            }
            Blocks = new ReadOnlyCollection<Block>(blockNameLookup.Select(kvp => kvp.Value).OrderBy(b => b.Name).ToList());
            BlockLookup = new ReadOnlyDictionary<string, Block>(blockNameLookup);
            foreach (var block in Blocks)
            {
                if (block.GetClass() != null)
                {
                    if (block.GetClass() == "Thruster")
                        thrusters.Add(block.Name, block);
                    else if (block.GetClass() == "Generator")
                        generators.Add(block.Name, block);
                }
            }
            Generators = new ReadOnlyDictionary<string, Block>(generators);
            Thrusters = new ReadOnlyDictionary<string, Block>(thrusters);
        }
    }
}

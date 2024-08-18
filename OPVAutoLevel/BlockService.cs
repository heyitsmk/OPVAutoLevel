using EgsLib.ConfigFiles;
using EgsLib.ConfigFiles.Ecf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace OPVAutoLevel
{
    public class BlockService
    {
        public ReadOnlyDictionary<string, Block> BlocksByName { get; private set; }
        public ReadOnlyCollection<Block> Blocks { get; private set; }
        public ReadOnlyDictionary<string, ReadOnlyCollection<Block>> BlocksByClass { get; private set; }
        public ReadOnlyCollection<string> Classes { get; private set; }
        public ReadOnlyDictionary<string, Block> Generators { get; private set; }
        public ReadOnlyDictionary<string, Block> Thrusters { get; private set; }

        public BlockService(string configPath)
        {
            var blockConfig = new EcfFile(configPath);
            var blockObjects = blockConfig.ParseObjects();
            var blockNameLookup = new Dictionary<string, Block>();
            var blockList = new List<Block>();
            foreach (var blockObject in blockObjects)
            {
                var block = new Block(this, blockObject);
                blockNameLookup.Add(block.Name, block);
            }
            Blocks = new ReadOnlyCollection<Block>(blockNameLookup.Select(kvp => kvp.Value).OrderBy(b => b.Name).ToList());
            BlocksByName = new ReadOnlyDictionary<string, Block>(blockNameLookup);
            var blockClassLookup = new Dictionary<string, List<Block>>();
            var thrusterLookup = new Dictionary<string, Block>();
            var generatorLookup = new Dictionary<string, Block>();
            foreach (var block in Blocks)
            {
                var blockClasses = block.GetClasses();
                foreach (var blockClass in blockClasses)
                {
                    if (blockClassLookup.TryGetValue(blockClass, out List<Block>? value))
                        value.Add(block);
                    else
                        blockClassLookup.Add(blockClass, new List<Block>() { block });
                    switch(blockClass)
                    {
                        case "Thruster":
                            if (!thrusterLookup.TryGetValue(block.Name, out var tblock))
                                thrusterLookup.Add(block.Name, block);
                            break;
                        case "Generator":
                            if (!generatorLookup.TryGetValue(block.Name, out var gblock))
                                generatorLookup.Add(block.Name, block);
                            break;
                    }                        
                }
            }
            BlocksByClass = new ReadOnlyDictionary<string, ReadOnlyCollection<Block>>(
                blockClassLookup.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ReadOnlyCollection<Block>(kvp.Value)
                ));
            Generators = new ReadOnlyDictionary<string, Block>(generatorLookup);
            Thrusters = new ReadOnlyDictionary<string, Block>(thrusterLookup);
            Classes = new ReadOnlyCollection<string>(BlocksByClass.Select(kvp => kvp.Key).ToList());
        }
    }
}

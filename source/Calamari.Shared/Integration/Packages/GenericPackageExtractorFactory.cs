﻿using System.Collections.Generic;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Java;
using Calamari.Integration.Processes;
using Calamari.Integration.ServiceMessages;

namespace Calamari.Integration.Packages
{
    public class GenericPackageExtractorFactory : IGenericPackageExtractorFactory
    {
        public GenericPackageExtractor createStandardGenericPackageExtractor()
        {
            return new GenericPackageExtractor();
        }

        public GenericPackageExtractor createJavaGenericPackageExtractor(ICalamariFileSystem fileSystem)
        {
            var commandOutput = new SplitCommandOutput(new ConsoleCommandOutput(),
                new ServiceMessageCommandOutput(
                    new CalamariVariableDictionary()));
            return new GenericPackageExtractor(new List<IPackageExtractor>
            {                            
                new JarExtractor(new CommandLineRunner(commandOutput), commandOutput, fileSystem)
            });
        }
    }
}
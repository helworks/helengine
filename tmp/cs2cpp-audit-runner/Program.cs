using cs2.cpp;

CPPConversionOptions options = CPPConversionOptions.CreateDefault();
options.LoadNativeRuntimeMetadata = false;
options.WriteConversionReport = true;

CPPConversionRules rules = new CPPConversionRules();
CPPCodeConverter converter = new CPPCodeConverter(rules, options);
converter.AddCsproj("C:\\dev\\helengine\\engine\\helengine.core\\helengine.core.csproj");
converter.WriteOutput("C:\\dev\\helengine\\tmp\\helengine-core-cpp-regenerated");

namespace helengine {
    /// <summary>
    /// Loads processed or raw content from disk using processors selected by output type and file extension.
    /// </summary>
    public class ContentManager {
        /// <summary>
        /// Processor id used for the built-in UTF-8 text loader.
        /// </summary>
        const string TextContentProcessorId = "core.text-content";

        /// <summary>
        /// Processor id used for the built-in raw-byte loader.
        /// </summary>
        const string RawByteContentProcessorId = "core.raw-byte-content";

        /// <summary>
        /// Wildcard extension token used to match any file suffix.
        /// </summary>
        const string WildcardExtension = "*";

        /// <summary>
        /// Root directory used to resolve relative content paths.
        /// </summary>
        readonly string RootDirectoryPath;
        /// <summary>
        /// Registered processors keyed by stable identifier.
        /// </summary>
        readonly Dictionary<string, ContentProcessorRegistration> ProcessorRegistrationsById;
        /// <summary>
        /// Default processors keyed by output type and normalized file extension.
        /// </summary>
        readonly Dictionary<Type, Dictionary<string, ContentProcessorRegistration>> DefaultProcessorsByTypeAndExtension;

        /// <summary>
        /// Initializes a new content manager rooted at the provided directory.
        /// </summary>
        /// <param name="rootDirectory">Directory used to resolve relative content paths.</param>
        public ContentManager(string rootDirectory) {
            if (string.IsNullOrWhiteSpace(rootDirectory)) {
                throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));
            }

            RootDirectoryPath = Path.GetFullPath(rootDirectory);
            ProcessorRegistrationsById = new Dictionary<string, ContentProcessorRegistration>(StringComparer.OrdinalIgnoreCase);
            DefaultProcessorsByTypeAndExtension = new Dictionary<Type, Dictionary<string, ContentProcessorRegistration>>();
            RegisterBuiltInProcessors();
        }

        /// <summary>
        /// Gets the root directory used to resolve relative content paths.
        /// </summary>
        public string RootDirectory => RootDirectoryPath;

        /// <summary>
        /// Determines whether a processor id is already registered on this content manager.
        /// </summary>
        /// <param name="processorId">Stable identifier used to address the processor.</param>
        /// <returns>True when the processor id is already registered.</returns>
        public bool IsProcessorRegistered(string processorId) {
            if (string.IsNullOrWhiteSpace(processorId)) {
                throw new ArgumentException("Processor id must be provided.", nameof(processorId));
            }

            return ProcessorRegistrationsById.ContainsKey(processorId);
        }

        /// <summary>
        /// Registers a content processor together with its supported extensions.
        /// </summary>
        /// <param name="registration">Registration describing the processor and its extensions.</param>
        public void RegisterProcessor(ContentProcessorRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }
            if (ProcessorRegistrationsById.ContainsKey(registration.ProcessorId)) {
                throw new InvalidOperationException($"Content processor '{registration.ProcessorId}' is already registered.");
            }

            string[] extensions = registration.Extensions;
            if (extensions.Length > 0) {
                Dictionary<string, ContentProcessorRegistration> registrationsByExtension = GetOrCreateTypeRegistrationMap(registration.OutputType);
                for (int extensionIndex = 0; extensionIndex < extensions.Length; extensionIndex++) {
                    string extension = NormalizeExtension(extensions[extensionIndex]);
                    if (registrationsByExtension.ContainsKey(extension)) {
                        throw new InvalidOperationException(
                            $"A default content processor is already registered for type '{registration.OutputType.Name}' and extension '{extension}'.");
                    }

                    registrationsByExtension.Add(extension, registration);
                }
            }

            ProcessorRegistrationsById.Add(registration.ProcessorId, registration);
        }

        /// <summary>
        /// Registers a typed content processor together with its supported extensions.
        /// </summary>
        /// <typeparam name="T">Output type produced by the processor.</typeparam>
        /// <param name="processorId">Stable identifier used to select the processor explicitly.</param>
        /// <param name="processor">Processor instance that parses the content.</param>
        /// <param name="extensions">Optional supported file extensions, including or omitting the leading dot. Use <c>*</c> to match any extension.</param>
        public void RegisterProcessor<T>(string processorId, IContentProcessor<T> processor, string[] extensions = null) {
            if (processor == null) {
                throw new ArgumentNullException(nameof(processor));
            }

            RegisterProcessor(new ContentProcessorRegistration(processorId, processor, extensions));
        }

        /// <summary>
        /// Loads content from disk using an explicitly named processor or the default processor for the requested type and file extension.
        /// </summary>
        /// <typeparam name="T">Type of content to load.</typeparam>
        /// <param name="assetPath">Absolute path or root-relative path to the content file.</param>
        /// <param name="processorId">Optional processor identifier used to override default resolution.</param>
        /// <returns>Loaded content value.</returns>
        public T Load<T>(string assetPath, string processorId = null) {
            string fullPath = ResolveContentPath(assetPath);
            IContentProcessor<T> processor = ResolveProcessor<T>(fullPath, processorId);
            return LoadProcessedContent(fullPath, processor);
        }

        /// <summary>
        /// Loads content from disk using an explicitly supplied processor instance.
        /// </summary>
        /// <typeparam name="T">Type of content to load.</typeparam>
        /// <param name="assetPath">Absolute path or root-relative path to the content file.</param>
        /// <param name="processor">Processor instance used to parse the content.</param>
        /// <returns>Loaded content value.</returns>
        public T Load<T>(string assetPath, IContentProcessor<T> processor) {
            if (processor == null) {
                throw new ArgumentNullException(nameof(processor));
            }

            string fullPath = ResolveContentPath(assetPath);
            return LoadProcessedContent(fullPath, processor);
        }

        /// <summary>
        /// Loads processed content from disk using the supplied typed processor.
        /// </summary>
        /// <typeparam name="T">Type of content to load.</typeparam>
        /// <param name="fullPath">Absolute file system path to the content file.</param>
        /// <param name="processor">Processor instance used to parse the content.</param>
        /// <returns>Loaded content value.</returns>
        T LoadProcessedContent<T>(string fullPath, IContentProcessor<T> processor) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Content path must be provided.", nameof(fullPath));
            }
            if (processor == null) {
                throw new ArgumentNullException(nameof(processor));
            }

            using FileStream stream = File.OpenRead(fullPath);
            return processor.Read(stream);
        }

        /// <summary>
        /// Resolves the processor used for one content load.
        /// </summary>
        /// <typeparam name="T">Requested output type.</typeparam>
        /// <param name="fullPath">Absolute file path to load.</param>
        /// <param name="processorId">Optional explicit processor identifier.</param>
        /// <returns>Typed processor instance.</returns>
        IContentProcessor<T> ResolveProcessor<T>(string fullPath, string processorId) {
            ContentProcessorRegistration registration = string.IsNullOrWhiteSpace(processorId)
                ? ResolveDefaultProcessorRegistration(typeof(T), fullPath)
                : ResolveExplicitProcessorRegistration(typeof(T), processorId);
            if (registration.Processor is IContentProcessor<T> typedProcessor) {
                return typedProcessor;
            }

            throw new InvalidOperationException(
                $"Registered processor '{registration.ProcessorId}' does not implement the expected processor interface for type '{typeof(T).Name}'.");
        }

        /// <summary>
        /// Resolves the default processor registration for a type and file path.
        /// </summary>
        /// <param name="requestedType">Requested output type.</param>
        /// <param name="fullPath">Absolute file path being loaded.</param>
        /// <returns>Matching processor registration.</returns>
        ContentProcessorRegistration ResolveDefaultProcessorRegistration(Type requestedType, string fullPath) {
            if (requestedType == null) {
                throw new ArgumentNullException(nameof(requestedType));
            }
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Content path must be provided.", nameof(fullPath));
            }

            if (!DefaultProcessorsByTypeAndExtension.TryGetValue(requestedType, out Dictionary<string, ContentProcessorRegistration> registrationsByExtension)) {
                throw new InvalidOperationException($"No content processors are registered for type '{requestedType.Name}'.");
            }

            string fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new InvalidOperationException($"Unable to resolve a content processor for '{requestedType.Name}' because '{fullPath}' does not contain a file name.");
            }

            string extension;
            if (!TryResolveRegisteredExtension(fileName, registrationsByExtension, out extension)) {
                throw new InvalidOperationException(
                    $"No content processor is registered for type '{requestedType.Name}' and file '{fileName}'.");
            }

            if (registrationsByExtension.TryGetValue(extension, out ContentProcessorRegistration registration)) {
                return registration;
            }

            throw new InvalidOperationException(
                $"No content processor is registered for type '{requestedType.Name}' and extension '{extension}'.");
        }

        /// <summary>
        /// Resolves an explicitly named processor registration and validates its output type.
        /// </summary>
        /// <param name="requestedType">Requested output type.</param>
        /// <param name="processorId">Explicit processor identifier.</param>
        /// <returns>Matching processor registration.</returns>
        ContentProcessorRegistration ResolveExplicitProcessorRegistration(Type requestedType, string processorId) {
            if (requestedType == null) {
                throw new ArgumentNullException(nameof(requestedType));
            }
            if (string.IsNullOrWhiteSpace(processorId)) {
                throw new ArgumentException("Processor id must be provided.", nameof(processorId));
            }

            if (!ProcessorRegistrationsById.TryGetValue(processorId, out ContentProcessorRegistration registration)) {
                throw new InvalidOperationException($"Content processor '{processorId}' is not registered.");
            }
            if (registration.OutputType != requestedType) {
                throw new InvalidOperationException(
                    $"Content processor '{processorId}' produces '{registration.OutputType.Name}', not '{requestedType.Name}'.");
            }

            return registration;
        }

        /// <summary>
        /// Gets or creates the default registration map for one output type.
        /// </summary>
        /// <param name="outputType">Output type whose registration map is required.</param>
        /// <returns>Registration map keyed by normalized extension.</returns>
        Dictionary<string, ContentProcessorRegistration> GetOrCreateTypeRegistrationMap(Type outputType) {
            if (outputType == null) {
                throw new ArgumentNullException(nameof(outputType));
            }

            if (DefaultProcessorsByTypeAndExtension.TryGetValue(outputType, out Dictionary<string, ContentProcessorRegistration> registrationsByExtension)) {
                return registrationsByExtension;
            }

            registrationsByExtension = new Dictionary<string, ContentProcessorRegistration>(StringComparer.OrdinalIgnoreCase);
            DefaultProcessorsByTypeAndExtension.Add(outputType, registrationsByExtension);
            return registrationsByExtension;
        }

        /// <summary>
        /// Resolves a relative or absolute content path to an absolute file system path.
        /// </summary>
        /// <param name="assetPath">Absolute path or root-relative path to the content file.</param>
        /// <returns>Absolute file system path.</returns>
        string ResolveContentPath(string assetPath) {
            if (string.IsNullOrWhiteSpace(assetPath)) {
                throw new ArgumentException("Asset path must be provided.", nameof(assetPath));
            }

            if (Path.IsPathRooted(assetPath)) {
                return Path.GetFullPath(assetPath);
            }

            return Path.GetFullPath(Path.Combine(RootDirectoryPath, assetPath));
        }

        /// <summary>
        /// Resolves the longest registered extension that matches a file name suffix.
        /// </summary>
        /// <param name="fileName">File name whose suffix should be matched.</param>
        /// <param name="registrationsByExtension">Registered extensions keyed to processor registrations.</param>
        /// <param name="matchedExtension">Longest matching registered extension when found.</param>
        /// <returns>True when a matching extension was found.</returns>
        bool TryResolveRegisteredExtension(
            string fileName,
            IReadOnlyDictionary<string, ContentProcessorRegistration> registrationsByExtension,
            out string matchedExtension) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }
            if (registrationsByExtension == null) {
                throw new ArgumentNullException(nameof(registrationsByExtension));
            }

            string normalizedFileName = fileName.ToLowerInvariant();
            matchedExtension = string.Empty;
            foreach (string extension in registrationsByExtension.Keys) {
                if (string.Equals(extension, WildcardExtension, StringComparison.Ordinal)) {
                    if (string.IsNullOrWhiteSpace(matchedExtension)) {
                        matchedExtension = extension;
                    }

                    continue;
                }

                if (!normalizedFileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (matchedExtension.Length >= extension.Length) {
                    continue;
                }

                matchedExtension = extension;
            }

            return !string.IsNullOrWhiteSpace(matchedExtension);
        }

        /// <summary>
        /// Normalizes an extension to lowercase text with a leading dot, preserving the wildcard token.
        /// </summary>
        /// <param name="extension">Extension value to normalize.</param>
        /// <returns>Normalized extension.</returns>
        string NormalizeExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.Equals(extension, WildcardExtension, StringComparison.Ordinal)) {
                return extension;
            }

            if (!extension.StartsWith(".")) {
                extension = "." + extension;
            }

            return extension.ToLowerInvariant();
        }

        /// <summary>
        /// Registers the built-in raw text and raw byte processors available on every content manager.
        /// </summary>
        void RegisterBuiltInProcessors() {
            RegisterProcessor(
                TextContentProcessorId,
                new TextContentProcessor(),
                new[] { WildcardExtension });
            RegisterProcessor(
                RawByteContentProcessorId,
                new RawByteContentProcessor(),
                new[] { WildcardExtension });
        }
    }
}

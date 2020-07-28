namespace ResxToJsonNg.MSBuild
{
    using Microsoft.Build.Framework;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel.Design;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Resources;
    using System.Web.Script.Serialization;

    /// <summary>
    /// Build task to convert Resource file to Java script Object Notation file
    /// </summary>
    public class ResxToJsonNgTask : ITask
    {
        /// <summary>
        /// Gets or sets Build Engine
        /// </summary>
        public IBuildEngine BuildEngine { get; set; }

        /// <summary>
        /// Gets or sets Host Object
        /// </summary>
        public ITaskHost HostObject { get; set; }

        /// <summary>
        /// Gets or sets list of EmbeddedResource Files
        /// </summary>
        [Required]
        public ITaskItem[] EmbeddedResourcesItems { get; set; }

        /// <summary>
        /// Gets or sets Project Full Path
        /// </summary>
        [Required]
        public string ProjectPath { get; set; }

        /// <summary>
        /// Gets or sets Project Output Path
        /// </summary>
        [Required]
        public string OutputPath { get; set; }

        /// <summary>
        /// Gets or sets Assembly Name
        /// </summary>
        [Required]
        public ITaskItem AssemblyName { get; set; }

        private long timestamp;

        /// <summary>
        /// Executes the Task
        /// </summary>
        /// <returns>True if success</returns>
        public bool Execute()
        {
            if (!EmbeddedResourcesItems.Any())
            {
                BuildEngine.LogMessageEvent(
                    new BuildMessageEventArgs(
                        string.Format(
                            "Skipping conversion of Resource files to json, as there are no resource files found in the project. If your resx file is not being picked up, check if the file is marked for build action = 'Embedded Resource'"),
                        string.Empty,
                        "ResxToJsonNg",
                        MessageImportance.Normal));
                return false;
            }

            timestamp = DateTime.UtcNow.ToFileTimeUtc();

            var args = new BuildMessageEventArgs(
                "Started converting Resx To JSON",
                string.Empty,
                "ResxToJsonNg",
                MessageImportance.Normal);

            var outputFullPath = Path.Combine(ProjectPath, OutputPath);

            BuildEngine.LogMessageEvent(args);
            foreach (var embeddedResourcesItem in EmbeddedResourcesItems)
            {
                BuildEngine.LogMessageEvent(
                    new BuildMessageEventArgs(
                        string.Format("Started converting Resx {0}", embeddedResourcesItem.ItemSpec),
                        string.Empty,
                        "ResxToJsonNg",
                        MessageImportance.Normal));

                var outputFileName = Path.GetFileNameWithoutExtension(embeddedResourcesItem.ItemSpec);
                outputFileName = outputFileName.Substring(outputFileName.IndexOf(".") + 1) + ".json";
                //if (!string.IsNullOrEmpty(this.AssemblyName.ItemSpec))
                //{
                //    outputFileName = this.AssemblyName.ItemSpec + "." + outputFileName;
                //}

                var outputFilePath = Path.Combine(
                    outputFullPath,
                    outputFileName);

                var content = GetJsonContent(embeddedResourcesItem.ItemSpec);

                using (var file = new StreamWriter(outputFilePath))
                {
                    file.Write(content);
                }

                // make a copy in the project path
                var sourceFilePath = Path.Combine(
                   ProjectPath,
                   outputFileName);
                File.Copy(outputFilePath, sourceFilePath, true);

                BuildEngine.LogMessageEvent(
                    new BuildMessageEventArgs(
                        string.Format("Generated file {0}", outputFileName),
                        string.Empty,
                        "ResxToJsonNg",
                        MessageImportance.Normal));
            }

            return true;
        }

        /// <summary>
        /// The get JSON content.
        /// </summary>
        /// <param name="resourceItem">
        /// The resource item.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private string GetJsonContent(string resourceItem)
        {
            var cultureInfo = GetCultureInfo(resourceItem);

            return GetJson(resourceItem, cultureInfo);
        }

        /// <summary>
        /// The get JSON from the resource.
        /// </summary>
        /// <param name="resourceItem">
        /// The resource item.
        /// </param>
        /// <param name="cultureInfo">
        /// The culture Info.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private string GetJson(string resourceItem, CultureInfo cultureInfo)
        {
            Dictionary<string, object> strings;
            using (var rsxr = new ResXResourceReader(resourceItem))
            {
                rsxr.UseResXDataNodes = true;
                strings = rsxr.Cast<DictionaryEntry>()
                    .ToDictionary(
                        x => x.Key.ToString(),
                        x => ((ResXDataNode)x.Value).GetValue((ITypeResolutionService)null));
            }

            strings.Add("lcid", cultureInfo == null ? 0 : cultureInfo.LCID);
            strings.Add("lang", cultureInfo == null ? string.Empty : cultureInfo.Name);
            strings.Add("r2jng", timestamp);

            return new JavaScriptSerializer().Serialize(strings);
        }

        /// <summary>
        /// The get culture info.
        /// </summary>
        /// <param name="resourceItem">
        /// The resource item.
        /// </param>
        /// <returns>
        /// The <see cref="CultureInfo"/>.
        /// </returns>
        private CultureInfo GetCultureInfo(string resourceItem)
        {
            var fileName = Path.GetFileNameWithoutExtension(resourceItem);

            // assuming the file name is of the format xyz.en-us.resx, xyx.abc.en-us.resx or xyx.resx
            var lang = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(lang))
            {
                try
                {
                    return new CultureInfo(lang.Trim('.'));
                }
                catch (Exception)
                {
                }
            }
            return null;
        }
    }
}

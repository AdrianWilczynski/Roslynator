﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Roslynator.CodeFixes;
using Roslynator.CSharp;

namespace Roslynator.Configuration
{
    public class Settings
    {
        public const string ConfigFileName = "roslynator.config";

        public Settings(
            IEnumerable<KeyValuePair<string, bool>> refactorings = null,
            IEnumerable<KeyValuePair<string, bool>> codeFixes = null,
            bool prefixFieldIdentifierWithUnderscore = false)
        {
            Initialize(Refactorings, refactorings);
            Initialize(CodeFixes, codeFixes);

            PrefixFieldIdentifierWithUnderscore = prefixFieldIdentifierWithUnderscore;

            void Initialize<T>(Dictionary<T, bool> dic, IEnumerable<KeyValuePair<T, bool>> values)
            {
                if (values != null)
                {
                    foreach (KeyValuePair<T, bool> kvp in values)
                        dic.Add(kvp.Key, kvp.Value);
                }
            }
        }

        public Dictionary<string, bool> Refactorings { get; } = new Dictionary<string, bool>(StringComparer.Ordinal);

        public Dictionary<string, bool> CodeFixes { get; } = new Dictionary<string, bool>();

        public bool PrefixFieldIdentifierWithUnderscore { get; set; }

        public static Settings Load(string path)
        {
            var settings = new Settings();

            XDocument doc = XDocument.Load(path);

            XElement root = doc.Element("Roslynator");

            if (root != null)
            {
                foreach (XElement element in root.Elements())
                {
                    XName name = element.Name;

                    if (name == "Settings")
                        LoadSettingsElement(element, settings);
                }
            }

            return settings;
        }

        private static void LoadSettingsElement(XElement element, Settings settings)
        {
            foreach (XElement child in element.Elements())
            {
                XName name = child.Name;

                if (name == "General")
                {
                    LoadGeneral(child, settings);
                }
                else if (name == "Refactorings")
                {
                    LoadRefactorings(child, settings);
                }
                else if (name == "CodeFixes")
                {
                    LoadCodeFixes(child, settings);
                }
            }
        }

        private static void LoadGeneral(XElement parent, Settings settings)
        {
            string value = parent.Element("PrefixFieldIdentifierWithUnderscore")?.Value;

            if (bool.TryParse(value, out bool result))
            {
                settings.PrefixFieldIdentifierWithUnderscore = result;
            }
        }

        private static void LoadRefactorings(XElement element, Settings settings)
        {
            foreach (XElement child in element.Elements("Refactoring"))
            {
                if (child.TryGetAttributeValueAsString("Id", out string id)
                    && child.TryGetAttributeValueAsBoolean("IsEnabled", out bool isEnabled))
                {
                    settings.Refactorings[id] = isEnabled;
                }
            }
        }

        private static void LoadCodeFixes(XElement element, Settings settings)
        {
            foreach (XElement child in element.Elements("CodeFix"))
            {
                if (child.TryGetAttributeValueAsString("Id", out string id)
                    && child.TryGetAttributeValueAsBoolean("IsEnabled", out bool isEnabled))
                {
                    if (id.Contains("."))
                    {
                        settings.CodeFixes[id] = isEnabled;
                    }
                    else if (id.StartsWith(CodeFixIdentifier.CodeFixIdPrefix, StringComparison.Ordinal))
                    {
                        foreach (string compilerDiagnosticId in CodeFixMap.GetCompilerDiagnosticIds(id))
                        {
                            settings.CodeFixes[$"{compilerDiagnosticId}.{id}"] = isEnabled;
                        }
                    }
                    else if (id.StartsWith("CS", StringComparison.Ordinal))
                    {
                        foreach (string codeFixId in CodeFixMap.GetCodeFixIds(id))
                        {
                            settings.CodeFixes[$"{id}.{codeFixId}"] = isEnabled;
                        }
                    }
                    else
                    {
                        Debug.Fail(id);
                    }
                }
            }
        }

        public void Save(string path)
        {
            var settings = new XElement("Settings",
                new XElement("General",
                    new XElement("PrefixFieldIdentifierWithUnderscore", PrefixFieldIdentifierWithUnderscore)));

            if (Refactorings.Any(f => !f.Value))
            {
                settings.Add(
                    new XElement("Refactorings",
                        Refactorings
                            .Where(f => !f.Value)
                            .OrderBy(f => f.Key)
                            .Select(f => new XElement("Refactoring", new XAttribute("Id", f.Key), new XAttribute("IsEnabled", f.Value)))
                    ));
            }

            if (CodeFixes.Any(f => !f.Value))
            {
                settings.Add(
                    new XElement("CodeFixes",
                        CodeFixes
                            .Where(f => !f.Value)
                            .OrderBy(f => f.Key)
                            .Select(f => new XElement("CodeFix", new XAttribute("Id", f.Key), new XAttribute("IsEnabled", f.Value)))
                    ));
            }

            var doc = new XDocument(new XElement("Roslynator", settings));

            var xmlWriterSettings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = false,
                NewLineChars = Environment.NewLine,
                IndentChars = "  ",
                Indent = true,
            };

            using (var fileStream = new FileStream(path, FileMode.Create))
            using (var streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
            using (XmlWriter xmlWriter = XmlWriter.Create(streamWriter, xmlWriterSettings))
                doc.WriteTo(xmlWriter);
        }
    }
}

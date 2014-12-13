﻿
namespace Sparkle.LinkedInNET.ServiceDefinition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    public class CSharpGenerator
    {
        private readonly TextWriter text;
        private string rootNamespace = "Sparkle.LinkedInNET";
        private static Regex urlParametersRegex = new Regex("\\{(?:([^{}= ]+) +)?([^{}= ]+)(?: *= *([^}]+))?\\}", RegexOptions.Compiled);
        private static readonly string[] csharpTypes = new string[] { "string", "int", "short", "long", "Guid", "DateTime", "double", "float", "byte", };

        public CSharpGenerator(TextWriter text)
        {
            this.text = text;
        }

        public string RootNamespace
        {
            get { return this.rootNamespace; }
            set { this.rootNamespace = value; }
        }

        public void Run(ServiceDefinition definition)
        {
            var context = new GeneratorContext();
            context.Definition = definition;
            context.Root = definition.Root;

            this.WriteEverything(context);

            text.Flush();
        }

        private void WriteEverything(GeneratorContext context)
        {
            ////// write common return types
            ////foreach (var returnType in context.Root.ReturnTypes.ToArray())
            ////{
            ////    var apiGroup = new ApiGroup
            ////    {
            ////        Name = "Common",
            ////    };
            ////    this.WriteReturnTypes(context, returnType, apiGroup);
            ////}

            foreach (var apiGroup in context.Root.ApiGroups)
            {
                // write all return types
                foreach (var returnType in apiGroup.ReturnTypes.ToArray())
                {
                    this.WriteReturnTypes(context, returnType, apiGroup);
                }

                this.WriteReturnTypeFields(context, apiGroup, apiGroup.ReturnTypes.ToArray());
            }

            foreach (var apiGroup in context.Root.ApiGroups)
            {
                // write client class
                this.WriteApiGroup(context, apiGroup);
            }

            this.WriteRootServices(context);
        }

        private void WriteReturnTypeFields(GeneratorContext context, ApiGroup apiGroup, ReturnType[] returnTypes)
        {
            int indent = 0;
            this.text.WriteLine(indent, "// WriteReturnTypeFields(" + apiGroup.Name + ")");
            this.text.WriteLine(indent, "namespace " + this.RootNamespace + "." + apiGroup.Name);
            this.text.WriteLine(indent++, "{");
            this.WriteNamespace(indent, "System");
            this.WriteNamespace(indent, "System.Collections.Generic");
            this.WriteNamespace(indent, "System.Xml.Serialization");
            this.text.WriteLine();

            this.text.WriteLine(indent, "/// <summary>");
            this.text.WriteLine(indent, "/// Field selectors for the '" + string.Join("', '", returnTypes.Select(r => r.Name).ToArray()) + "' return types.");
            this.text.WriteLine(indent, "/// </summary>");
            this.text.WriteLine(indent++, "public static class " + apiGroup.Name + "Fields {");

            foreach (var returnType in returnTypes)
            {
                ////var returnTypeName = this.GetPropertyName(returnType.ClassName, returnType.Name);
                ////this.text.WriteLine(indent++, "public static FieldSelector<" + returnTypeName + "> Fields(this FieldSelector<" + returnTypeName + "> me) {");
                ////this.text.WriteLine(indent, "return me.Add(\"" + field.Name + "\");");
                ////this.text.WriteLine(--indent, "}");
                ////this.text.WriteLine(indent, "");
                var returnTypeName = this.GetPropertyName(returnType.ClassName, returnType.Name);

                ////this.text.WriteLine(indent, "// return type: " + (returnType.ClassName ?? returnType.Name));

                var allFields = new List<string>();
                foreach (var fieldGroup in returnType.Fields.GroupBy(f => f.Name).ToArray())
                {
                    var field = fieldGroup.First();
                    var selectors = fieldGroup.Where(f => f.Selectors != null).SelectMany(f => f.Selectors).ToArray();

                    if (selectors.Length == 0 && returnType.AutoGenerateFieldSelectors)
                    {
                        var name = field.Name;
                        var fieldName = this.GetPropertyName(null, field.Name);
                        allFields.Add(name);

                        WriteReturnTypeField(indent, returnTypeName, fieldName, name);
                    }
                    else
                    {
                        foreach (var selector in selectors)
                        {
                            var name = selector.Name;
                            var fieldName = this.GetPropertyName(selector.PropertyName, name);
                            allFields.Add(name);

                            WriteReturnTypeField(indent, returnTypeName, fieldName, name);
                        }
                    }
                }

                if (returnType.Selectors != null)
                {
                    foreach (var customSelector in returnType.Selectors)
                    {
                        if (!allFields.Contains(customSelector.Name))
                        {
                            var fieldName = this.GetPropertyName(customSelector.PropertyName, customSelector.Name);
                            WriteReturnTypeField(indent, returnTypeName, fieldName, customSelector.Name);

                            allFields.Add(customSelector.Name);
                        }
                    }
                }

                if (returnType.ImportFieldSelectors != null)
                {
                    foreach (var import in returnType.ImportFieldSelectors)
                    {
                        if (import.ReturnType != null)
                        {
                            var type = context.Definition.FindReturnType(import.ReturnType);
                            if (type != null)
                            {
                                foreach (var field in type.Fields)
                                {
                                    var fullName = import.Name + ":(" + field.Name + ")";
                                    var fullPropertyName = this.GetPropertyName(null, import.Name) + this.GetPropertyName(field.PropertyName, field.Name);
                                    WriteReturnTypeField(indent, returnTypeName, fullPropertyName, fullName);
                                    allFields.Add(fullName);
                                }
                            }
                        }
                    }
                }

                if (allFields.Count > 0)
                {
                    this.text.WriteLine(indent, "/// <summary>");
                    this.text.WriteLine(indent, "/// Includes all the fields.");
                    this.text.WriteLine(indent, "/// </summary>");
                    this.text.WriteLine(indent, "/// <param name=\"me\">The field selector.</param>");
                    this.text.WriteLine(indent, "/// <returns>The field selector.</returns>");
                    this.text.Write(indent++, "public static FieldSelector<" + returnTypeName + "> WithAllFields(this FieldSelector<" + returnTypeName + "> me) { ");
                    this.text.Write(@"return me.AddRange(""");
                    var sep = "";
                    for (int i = 0; i < allFields.Count; i++)
                    {
                        var name = allFields[i];
                        var slash = name.IndexOf('/');
                        if (slash > 0)
                            name = name.Substring(0, slash);

                        this.text.Write(sep);
                        this.text.Write(name);
                        sep = "\", \"";
                    }

                    this.text.Write("\"); ");
                    this.text.WriteLine("}");
                    indent--;
                    this.text.WriteLine(indent, "");
                }
                else
                {
                    ////this.text.WriteLine(indent, "// allFields.Count == 0");
                }

                ////this.text.WriteLine(indent, "");
            }

            this.text.WriteLine(--indent, "}");
            this.text.WriteLine(--indent, "}");
            this.text.WriteLine();
        }

        private void WriteReturnTypeField(int indent, string returnTypeName, string fieldName, string name)
        {
            var slash = name.IndexOf('/');
            if (slash > 0)
                name = name.Substring(0, slash);

            this.text.WriteLine(indent, "/// <summary>");
            this.text.WriteLine(indent, "/// Includes the field '" + name + "'.");
            this.text.WriteLine(indent, "/// </summary>");
            this.text.WriteLine(indent, "/// <param name=\"me\">The field selector.</param>");
            this.text.WriteLine(indent, "/// <returns>The field selector.</returns>");
            this.text.Write(indent++, "public static FieldSelector<" + returnTypeName + "> With" + fieldName + "(this FieldSelector<" + returnTypeName + "> me) { ");
            this.text.Write("return me.Add(\"" + name + "\"); ");
            this.text.WriteLine("}");
            indent--;
            this.text.WriteLine(indent, "");
        }

        private void WriteRootServices(GeneratorContext context)
        {
            int indent = 0;
            this.text.WriteLine(indent, "// WriteRootServices()");
            this.text.WriteLine(indent, "namespace " + this.RootNamespace);
            this.text.WriteLine(indent++, "{");
            this.WriteNamespace(indent, "System");
            this.WriteNamespace(indent, "System.Xml.Serialization");
            this.WriteNamespace(indent, this.RootNamespace + ".Internals");

            foreach (var item in context.Root.ApiGroups)
            {
                var name = this.GetPropertyName(null, item.Name);
                this.WriteNamespace(indent, this.RootNamespace + "." + name);
            }

            this.text.WriteLine();
            this.text.WriteLine(indent, "/// <summary>");
            this.text.WriteLine(indent, "/// The factory for LinkedIn APIs.");
            this.text.WriteLine(indent, "/// </summary>");
            this.text.WriteLine(indent, "public partial class LinkedInApi : BaseApi");
            this.text.WriteLine(indent++, "{");

            // ctor


            // methods
            foreach (var item in context.Root.ApiGroups)
            {
                var name = this.GetPropertyName(null, item.Name);
                this.text.WriteLine(indent, "/// <summary>");
                this.text.WriteLine(indent, "/// The " + name + " API.");
                this.text.WriteLine(indent, "/// </summary>");
                this.text.WriteLine(indent++, "public " + name + "Api " + name + " {");
                this.text.WriteLine(indent, "[System.Diagnostics.DebuggerStepThrough]");
                this.text.WriteLine(indent, "get { return new " + name + "Api(this); }");
                this.text.WriteLine(--indent, "}");
                this.text.WriteLine();
            }

            this.text.WriteLine(--indent, "}");
            this.text.WriteLine(--indent, "}");
            this.text.WriteLine();

        }

        private void WriteApiGroup(GeneratorContext context, ApiGroup apiGroup)
        {
            var className = this.GetPropertyName(null, apiGroup.Name) + "Api";

            int indent = 0;
            this.text.WriteLine(indent, "// WriteApiGroup(" + apiGroup.Name + ")");
            this.text.WriteLine(indent, "namespace " + this.RootNamespace + "." + apiGroup.Name);
            this.text.WriteLine(indent++, "{");
            this.WriteNamespace(indent, "System");
            this.WriteNamespace(indent, "System.Xml.Serialization");
            this.WriteNamespace(indent, this.RootNamespace + ".Internals");
            this.text.WriteLine();
            this.text.WriteLine(indent, "/// <summary>");
            this.text.WriteLine(indent, "/// Name: '" + apiGroup.Name + "'");
            this.text.WriteLine(indent, "/// </summary>");
            this.text.WriteLine(indent, "public class " + className + " : BaseApi");
            this.text.WriteLine(indent++, "{");

            // ctor

            this.text.WriteLine(indent, "[System.Diagnostics.DebuggerStepThrough]");
            this.text.WriteLine(indent, "internal " + className + "(LinkedInApi linkedInApi)");
            this.text.WriteLine(indent, "    : base(linkedInApi)");
            this.text.WriteLine(indent++, "{");
            this.text.WriteLine(--indent, "}");
            this.text.WriteLine(indent, "");


            // methods
            WriteMethod(context, apiGroup, indent);

            this.text.WriteLine(--indent, "}");
            this.text.WriteLine(--indent, "}");
            this.text.WriteLine();
        }

        private void WriteMethod(GeneratorContext context, ApiGroup apiGroup, int indent)
        {
            foreach (var method in apiGroup.Methods)
            {
                var returnType = "void";
                ReturnType returnTypeType = null;
                if (method.ReturnType != null)
                {
                    returnTypeType = context.Definition.FindReturnType(method.ReturnType, apiGroup.Name);
                    if (returnTypeType != null)
                    {
                        returnType = this.GetPropertyName(returnTypeType.ClassName, returnTypeType.Name);
                    }
                }

                var parameters = new List<TupleStruct<string, string>>();

                if (method.RequiresUserAuthentication)
                {
                    parameters.Add(new TupleStruct<string, string>("UserAuthorization", "user"));
                }

                var urlParams = this.GetUrlPathParameters(method.Path, NameTransformation.PascalCase);
                foreach (var urlParam in urlParams)
                {
                    parameters.Add(new TupleStruct<string, string>(urlParam.Value.Type ?? "string", urlParam.Value.Name));
                }

                if (method.UsesAcceptLanguage)
                {
                    parameters.Add(new TupleStruct<string, string>("string[]", "acceptLanguages = null"));
                }

                // doc
                this.text.WriteLine(indent, "/// <summary>");
                this.text.WriteLine(indent, "/// " + method.Title);
                this.text.WriteLine(indent, "/// </summary>");

                // name and arguments
                this.text.WriteLine(indent++, "public " + returnType + " " + this.GetPropertyName(method.MethodName, method.Path) + "(");

                var sep = "  ";
                foreach (var parameter in parameters)
                {
                    this.text.WriteLine(indent, sep + parameter.Value1 + " " + parameter.Value2);
                    sep = ", ";
                }

                if (returnType != null)
                    this.text.WriteLine(indent, ", FieldSelector<" + returnType + "> fields = null");
                this.text.WriteLine(--indent, ")");
                this.text.WriteLine(indent++, "{");

                // body / format url
                if (urlParams.Count > 0)
                {
                    this.text.WriteLine(indent, "const string urlFormat = \"" + method.Path + "\";");
                    this.text.WriteLine(indent, "var url = FormatUrl(urlFormat, fields, " + string.Join(", ", urlParams.Values.Select(p => "\"" + p.OriginalName + "\", " + p.Name).ToArray()) + ");");
                }
                else if (method.Path.Contains("FieldSelector"))
                {
                    this.text.WriteLine(indent, "const string urlFormat = \"" + method.Path + "\";");
                    this.text.WriteLine(indent, "var url = FormatUrl(urlFormat, fields);");
                }
                else
                {
                    this.text.WriteLine(indent, "var url = \"" + method.Path + "\";");
                }

                // body / create context
                this.text.WriteLine();
                text.WriteLine(indent, "var context = new RequestContext();");

                if (method.RequiresUserAuthentication)
                {
                    text.WriteLine(indent, "context.UserAuthorization = user;");
                }

                if (method.UsesAcceptLanguage)
                {
                    text.WriteLine(indent, "context.AcceptLanguages = acceptLanguages;");
                }

                text.WriteLine(indent, "context.Method =  \"" + method.HttpMethod + "\";");
                text.WriteLine(indent, "context.UrlPath = this.LinkedInApi.Configuration.BaseApiUrl + url;");

                // body / execute
                this.text.WriteLine();
                text.WriteLine(indent++, "if (!this.ExecuteQuery(context))");
                ////text.WriteLine(indent--, "this.HandleXmlErrorResponse(context);");
                ////text.WriteLine(indent, "return this.HandleXmlResponse<" + returnType + ">(context);");
                text.WriteLine(indent--, "this.HandleJsonErrorResponse(context);");
                text.WriteLine(indent, "return this.HandleJsonResponse<" + returnType + ">(context);");

                // body / handle


                // body / return

                ////this.text.WriteLine(indent, "throw new NotImplementedException(url);");
                this.text.WriteLine(--indent, "}");
                this.text.WriteLine(indent, "");
            }
        }

        protected IDictionary<string, Parameter> GetUrlPathParameters(string path, NameTransformation transform = NameTransformation.None)
        {
            var values = new Dictionary<string, Parameter>();
            var matches = urlParametersRegex.Matches(path);
            foreach (Match match in matches)
            {
                var full = match.Groups[0].Captures[0].Value;
                full = full.Substring(1, full.Length - 2);
                var key = match.Groups[2].Captures[0].Value;
                var type = match.Groups[1].Success ? match.Groups[1].Captures[0].Value : null;
                var value = match.Groups[3].Success ? match.Groups[3].Captures[0].Value : null;
                if (key == "FieldSelector")
                    continue;

                var item = new Parameter
                {
                    OriginalName = full,
                    Name = Namify(key, transform),
                    Type = type,
                    Value = value,
                };

                values.Add(key, item);
            }

            return values;
        }

        private void WriteReturnTypes(GeneratorContext context, ReturnType returnType, ApiGroup apiGroup)
        {
            int indent = 0;
            this.text.WriteLine(indent, "// WriteReturnTypes(" + apiGroup.Name + ", " + returnType.Name + ")");
            this.text.WriteLine(indent, "namespace " + this.RootNamespace + "." + apiGroup.Name);
            this.text.WriteLine(indent++, "{");
            this.WriteNamespace(indent, "System");
            this.WriteNamespace(indent, "System.Collections.Generic");
            this.WriteNamespace(indent, "System.Xml.Serialization");
            this.WriteNamespace(indent, "Newtonsoft.Json");
            this.text.WriteLine();
            this.text.WriteLine(indent, "/// <summary>");
            this.text.WriteLine(indent, "/// Name: '" + returnType.Name + "'");
            this.text.WriteLine(indent, "/// </summary>");
            if (returnType.Remark != null)
            {
                this.text.WriteLine(indent, "/// <remarks>");
                this.text.WriteLine(indent, "/// " + returnType.Remark + "");
                this.text.WriteLine(indent, "/// </remarks>");
            }

            this.text.WriteLine(indent, "[Serializable, XmlRoot(\"" + returnType.Name + "\")]");
            this.text.WriteLine(indent, "public class " + this.GetPropertyName(returnType.ClassName, returnType.Name));
            this.text.WriteLine(indent++, "{");

            foreach (var itemGroup in returnType.Fields.GroupBy(f => f.FieldName.PropertyName).ToArray())
            {
                var item = itemGroup.First();
                ////var parts = item.Name.Split(new char[] { ':', }, 2);
                ////var mainPart = parts.Length == 1 ? parts[0] : parts[0];
                ////var subPart = parts.Length == 2 ? parts[1] : null;
                var isCollection = item.IsCollection;

                var type = item.Type ?? item.FieldName.ClassName ?? "string";
                ReturnType fieldReturnType = null;
                if (!csharpTypes.Contains(type))
                {
                    fieldReturnType = context.Definition.FindReturnType(type);
                    if (fieldReturnType != null)
                    {
                        type = fieldReturnType.ClassName ?? Namify(fieldReturnType.Name);
                    }
                }

                if (isCollection)
                {
                    type = "List<" + type + ">";
                }

                this.text.WriteLine(indent, "/// <summary>");
                foreach (var subItem in itemGroup)
                {
                    this.text.WriteLine(indent, "/// Field: '" + subItem.Name + "' (" + (subItem.IsDefault ? "default" : "on-demand") + ")");
                }

                foreach (var subItem in itemGroup.Where(f => f.Selectors != null).SelectMany(f => f.Selectors))
                {
                    this.text.WriteLine(indent, "/// Field: '" + subItem.Name + "'");
                }

                var xmlAttribute = item.IsAttribute ? "XmlAttribute" : "XmlElement";
                var xmlAttributeNameProp = item.IsAttribute ? "AttributeName" : "ElementName";
                var jsonName = (item.IsAttribute ? "_" : "") + Namify(item.FieldName.ApiName, NameTransformation.PascalCase);
                if (item.IsCollection)
                    jsonName = "values";

                this.text.WriteLine(indent, "/// </summary>");
                this.text.WriteLine(indent, (item.Ignore ? "////" : "") + "[" + xmlAttribute + "(" + xmlAttributeNameProp + " = \"" + item.FieldName.ApiName + "\")]");
                this.text.WriteLine(indent, (item.Ignore ? "////" : "") + "[JsonProperty(PropertyName = \"" + jsonName + "\")]");
                this.text.WriteLine(indent, "public " + type + " " + item.FieldName.PropertyName + " { get; set; }");
                this.text.WriteLine();
            }

            this.text.WriteLine(--indent, "}");
            this.text.WriteLine(--indent, "}");
            this.text.WriteLine();
        }

        private string GetPropertyName(string propertyName, string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (propertyName != null)
                return propertyName;

            var words = name.Split(new char[] { '-', '/', '(', ')', ':', }, StringSplitOptions.RemoveEmptyEntries);

            return string.Join("", words.Select(w => Namify(w, NameTransformation.CamelCase)).ToArray());
        }

        internal static string Namify(string value, NameTransformation transform = NameTransformation.CamelCase, string unifier = "")
        {
            var result = string.Empty;
            var words = value.Split(new char[] { '-', '/', });
            var newWords = new string[words.Length];

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if ((transform & NameTransformation.PascalCase) == NameTransformation.PascalCase)
                {
                    if (i == 0)
                        word = word[0].ToString().ToLowerInvariant() + new string(word.Skip(1).ToArray());
                    else
                        word = word[0].ToString().ToUpperInvariant() + new string(word.Skip(1).ToArray());
                }

                if ((transform & NameTransformation.CamelCase) == NameTransformation.CamelCase)
                {
                    word = word[0].ToString().ToUpperInvariant() + new string(word.Skip(1).ToArray());
                }

                newWords[i] = word;
            }


            return string.Join(unifier, newWords);
        }

        private void WriteNamespace(int indent, string value)
        {
            this.text.WriteLine(indent, "using " + value + ";");
        }

        public class GeneratorContext
        {
            public ApisRoot Root { get; set; }

            public ServiceDefinition Definition { get; set; }
        }
    }
}

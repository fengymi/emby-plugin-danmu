// namespace Emby.Plugin.Danmu
// {
//     using System.ComponentModel;
//
//     using Emby.Web.GenericEdit;
//     using Emby.Web.GenericEdit.Validation;
//
//     using MediaBrowser.Model.Attributes;
//     using MediaBrowser.Model.GenericEdit;
//     using MediaBrowser.Model.Logging;
//     using MediaBrowser.Model.MediaInfo;
//
//     public class PluginOptions : EditableOptionsBase
//     {
//         public override string EditorTitle => "Plugin Options";
//
//         public override string EditorDescription => "This is a description text, shown at the top of the options page.\n"
//                                                     + "The options below are just a few examples for creating UI elementsd.";
//
//         [DisplayName("Output Folder")]
//         [Description("Please choose a folder for plugin output")]
//         [EditFolderPicker]
//         public string TargetFolder { get; set; }
//
//         [Description("The log level determines how messages will be logged")]
//         public LogSeverity LogLevel { get; set; }
//
//         [Description("This value is required and needs to have a minimum length of 10")]
//         [MediaBrowser.Model.Attributes.Required]
//         public string MessageFormat { get; set; }
//
//         protected override void Validate(ValidationContext context)
//         {
//             if (!(this.MessageFormat?.Length >= 10))
//             {
//                 context.AddValidationError(nameof(this.MessageFormat), "Minimum length is 10 characters");
//             }
//         }
//     }
// }

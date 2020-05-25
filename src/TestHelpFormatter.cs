namespace WhMgr
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    using DSharpPlus.CommandsNext;
    using DSharpPlus.CommandsNext.Converters;
    using DSharpPlus.CommandsNext.Entities;

    public sealed class TestHelpFormatter : BaseHelpFormatter
    {
        private StringBuilder Content { get; }

        public TestHelpFormatter(CommandContext ctx)
            : base(ctx)
        {
            Content = new StringBuilder();
        }

        public override BaseHelpFormatter WithCommand(Command command)
        {
            Content.Append(command.Description ?? "No description provided.").Append("\n\n");

            if (command.Aliases?.Any() == true)
                Content.Append("Aliases: ").Append(string.Join(", ", command.Aliases)).Append("\n\n");

            if (command.Overloads?.Any() == true)
            {
                var sb = new StringBuilder();

                foreach (var ovl in command.Overloads.OrderByDescending(x => x.Priority))
                {
                    sb.Append(command.QualifiedName);

                    foreach (var arg in ovl.Arguments)
                        sb.Append(arg.IsOptional || arg.IsCatchAll ? " [" : " <").Append(arg.Name).Append(arg.IsCatchAll ? "..." : "").Append(arg.IsOptional || arg.IsCatchAll ? ']' : '>');

                    sb.Append('\n');

                    foreach (var arg in ovl.Arguments)
                        sb.Append(arg.Name).Append(" (").Append(CommandsNext.GetUserFriendlyTypeName(arg.Type)).Append("): ").Append(arg.Description ?? "No description provided.").Append('\n');

                    sb.Append('\n');
                }

                Content.Append("Arguments:\n").Append(sb.ToString());
            }

            return this;
        }

        public override BaseHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
        {
            if (Content.Length == 0)
                Content.Append("Displaying all available commands.\n\n");
            else
                Content.Append("Subcommands:\n");

            if (subcommands?.Any() == true)
            {
                var ml = subcommands.Max(xc => xc.Name.Length);
                var sb = new StringBuilder();
                foreach (var xc in subcommands)
                    sb.Append(xc.Name.PadRight(ml, ' '))
                        .Append("  ")
                        .Append(string.IsNullOrWhiteSpace(xc.Description) ? "" : xc.Description).Append("\n");
                Content.Append(sb.ToString());
            }

            return this;
        }

        public override CommandHelpMessage Build()
            => new CommandHelpMessage($"```less\n{Content.ToString().Trim()}\n```");
    }
}

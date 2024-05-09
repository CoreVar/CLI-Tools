using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Builders;

public interface ICommandOptionBuilder : ICommandElementBuilder
{

}

public interface ICommandOptionBuilder<T> : ICommandOptionBuilder
{
}
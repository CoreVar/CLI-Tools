using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class NestedCommandAttribute<T> : Attribute
{

}
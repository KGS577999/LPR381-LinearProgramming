using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LPR381
{
	internal class Program
	{
		public enum Options
		{
			Primal_Simplex = 1,
			Revised_Primal_Simplex,
			Branch_and_Bound,
			Knapsack_Branch_and_Bound,
			Cutting_Plane
		}

		static void Main(string[] args)
		{
			bool looping = true;

			do
			{
				Console.Clear();

				string path = "input.txt";
				string content = File.ReadAllText(path);

				// Split the content by lines
				string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

				string objectiveFunction = lines[0].Trim();

				string constraint = lines[1].Trim();

				//Console.WriteLine("Objective Function: " + objectiveFunction);
				//Console.WriteLine("Constraint: " + constraint);

				Algorithm algo = null;
				int menuNum = 1;
				Console.WriteLine("###### Choose an Algorithm #######\n");

				foreach (string option in Enum.GetNames(typeof(Options)))
				{
					Console.WriteLine($"{menuNum}. {option.Replace("_", " ")}");

					if (menuNum == 5)
					{
						Console.WriteLine();
					}
					else
					{
						menuNum++;
					}
				}

				Console.Write("Enter Choice: ");
				int chosenAlgo = int.Parse(Console.ReadLine());

				if(chosenAlgo < 1 || chosenAlgo > 5)
				{
					Console.WriteLine("Invalid choice, press any key to retry...");
					Console.ReadKey();
					continue;
				}

				switch ((Options)chosenAlgo)
				{
					case Options.Primal_Simplex:
						algo = new PrimalSimplex(objectiveFunction, constraint);
						break;
					case Options.Revised_Primal_Simplex:
						break;
					case Options.Branch_and_Bound:
						break;
					case Options.Knapsack_Branch_and_Bound:
						break;
					case Options.Cutting_Plane:
						break;
				}

				Console.ReadKey();

			} while (looping);
		}
	}
}

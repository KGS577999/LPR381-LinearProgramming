using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace LPR381
{
	public class PrimalSimplex: Algorithm
	{

		private string objective;
		private string[] constraints;

		public PrimalSimplex(string objective, string[] constarints)
		{
			this.objective = objective;
			this.constraints = constarints;
		}

		public void Solve()
		{
			Console.WriteLine("Running Primal Simplex...\n");

			// Parse objective function
			string[] objTokens = objective.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			double[] objCoeffs = objTokens.Skip(1).Select(double.Parse).ToArray();
			int numVars = objCoeffs.Length;

			// Parse constraints
			List<double[]> constraintCoeffs = new List<double[]>();
			List<double> rhsList = new List<double>();

			foreach (var constr in constraints)
			{
				string trimmed = constr.Trim();

				// Find operator position
				int opPos = trimmed.IndexOf("<=");
				string op = "<=";
				if (opPos == -1)
				{
					opPos = trimmed.IndexOf(">=");
					op = ">=";
				}
				if (opPos == -1)
				{
					opPos = trimmed.IndexOf("=");
					op = "=";
				}
				if (opPos == -1)
					throw new Exception("Constraint missing operator (<=, >=, =)");

				// Split into coefficients and RHS
				string coeffPart = trimmed.Substring(0, opPos).Trim();
				string rhsPart = trimmed.Substring(opPos + op.Length).Trim();

				double[] coeffs = coeffPart
					.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(double.Parse)
					.ToArray();

				if (coeffs.Length != numVars)
					throw new Exception($"Constraint has {coeffs.Length} coefficients, expected {numVars}");

				double rhs = double.Parse(rhsPart);

				// Convert >= to <=
				if (op == ">=")
				{
					for (int i = 0; i < coeffs.Length; i++) coeffs[i] *= -1;
					rhs *= -1;
				}

				constraintCoeffs.Add(coeffs);
				rhsList.Add(rhs);
			}

			int numConstraints = constraintCoeffs.Count;

			// Initialize tableau
			double[,] tableau = new double[numConstraints + 1, numVars + numConstraints + 1];

			// Fill tableau with constraints
			for (int i = 0; i < numConstraints; i++)
			{
				for (int j = 0; j < numVars; j++)
					tableau[i, j] = constraintCoeffs[i][j];

				tableau[i, numVars + i] = 1; // slack variable
				tableau[i, numVars + numConstraints] = rhsList[i]; // RHS
			}

			// Fill objective row
			for (int j = 0; j < numVars; j++)
				tableau[numConstraints, j] = -objCoeffs[j];

			tableau[numConstraints, numVars + numConstraints] = 0;

			// Simplex loop
			while (true)
			{
				int entering = FindEntering(tableau, numConstraints, numVars);
				if (entering < 0) break; // optimal

				int leaving = FindLeaving(tableau, entering, numConstraints, numVars);
				if (leaving < 0)
					throw new Exception("Unbounded solution");

				Pivot(tableau, leaving, entering, numConstraints, numVars);
			}

			// Print Solution
			PrintSolution(tableau, numConstraints, numVars);
		}

		private int FindEntering(double[,] tableau, int m, int n)
		{
			int col = -1;
			double min = 0;
			for (int j = 0; j < n + m; j++)
			{
				if (tableau[m, j] < min)
				{
					min = tableau[m, j];
					col = j;
				}
			}
			return col;
		}

		private int FindLeaving(double[,] tableau, int entering, int m, int n)
		{
			int row = -1;
			double minRatio = double.PositiveInfinity;

			for (int i = 0; i < m; i++)
			{
				if (tableau[i, entering] > 0)
				{
					double ratio = tableau[i, n + m] / tableau[i, entering];
					if (ratio < minRatio)
					{
						minRatio = ratio;
						row = i;
					}
				}
			}
			return row;
		}

		private void Pivot(double[,] tableau, int row, int col, int m, int n)
		{
			double pivot = tableau[row, col];
			int cols = n + m + 1;

			// normalize pivot row
			for (int j = 0; j < cols; j++)
				tableau[row, j] /= pivot;

			// eliminate other rows
			for (int i = 0; i <= m; i++)
			{
				if (i == row) continue;
				double factor = tableau[i, col];
				for (int j = 0; j < cols; j++)
					tableau[i, j] -= factor * tableau[row, j];
			}
		}

		private void PrintSolution(double[,] tableau, int m, int n)
		{
			double[] solution = new double[n];

			for (int j = 0; j < n; j++)
			{
				int pivotRow = -1;
				bool isBasic = true;

				for (int i = 0; i < m; i++)
				{
					if (Math.Abs(tableau[i, j] - 1) < 1e-6)
					{
						if (pivotRow == -1) pivotRow = i;
						else { isBasic = false; break; }
					}
					else if (Math.Abs(tableau[i, j]) > 1e-6)
					{
						isBasic = false;
						break;
					}
				}

				if (isBasic && pivotRow != -1)
					solution[j] = tableau[pivotRow, n + m];
				else
					solution[j] = 0;
			}

			Console.WriteLine("Optimal Solution:");
			for (int j = 0; j < n; j++)
				Console.WriteLine($"x{j + 1} = {solution[j]}");

			Console.WriteLine($"\nOptimal Objective Value = {tableau[m, n + m]}");
		}
	}
}

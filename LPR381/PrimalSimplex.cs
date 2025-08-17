using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace LPR381
{
	public class PrimalSimplex: Algorithm
	{

		private string objective;
		private string constarint;

		public PrimalSimplex(string objective, string constarint)
		{
			this.objective = objective;
			this.constarint = constarint;
		}

		public void Solve()
		{
			Console.WriteLine("Primal Simplex");
		}
	}
}

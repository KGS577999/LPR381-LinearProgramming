using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LPR381
{
    // Kept the same base type as our PrimalSimplex so Program.cs can call it the same way.
    public class BranchBoundSimplex
    {
        private readonly string _objLine;
        private readonly string[] _constraintLines;

        public BranchBoundSimplex(string objectiveFunction, string[] constraintLines)
        {
            _objLine = objectiveFunction;
            _constraintLines = constraintLines;
        }

        public void Solve()
        {
            // Parse model once
            Model root = ParseModel(_objLine, _constraintLines);

            // Basic checks
            if (root.Sense != Sense.Max)
            {
                Console.WriteLine("Only max problems are supported in this single-file demo.");
                return;
            }
            if (root.Relations.Any(r => r != Rel.LessEqual))
            {
                Console.WriteLine("Only <= constraints are supported in this single-file demo.");
                return;
            }
            if (root.Signs.Any(s => s != Sign.Plus))
            {
                Console.WriteLine("Only non-negative variables (x >= 0) are supported in this demo.");
                return;
            }

            // Branch & Bound (best-first by LP bound)
            double bestObj = double.NegativeInfinity;
            double[] bestX = null;

            List<Node> open = new List<Node>();
            open.Add(new Node { Model = root, Tag = "root" });

            int nodeId = 1;

            while (open.Count > 0)
            {
                // Pop node with best LP bound if already evaluated, else FIFO
                int pick = PickBest(open);
                Node cur = open[pick];
                open.RemoveAt(pick);

                // Solve LP relaxation for the node
                LpResult lp = SolveLp(cur.Model);
                cur.Bound = lp.Status == LpStatus.Optimal ? lp.Objective : double.NegativeInfinity;

                Console.WriteLine();
                Console.WriteLine($"[Node {nodeId++} – {cur.Tag}] LP status: {lp.Status}, Bound z={lp.Objective:0.###}");

                if (lp.Status == LpStatus.Infeasible)
                {
                    Console.WriteLine("  Fathomed: infeasible.");
                    continue;
                }

                // Bound pruning
                if (lp.Objective <= bestObj + 1e-12)
                {
                    Console.WriteLine($"  Fathomed: bound {lp.Objective:0.###} ≤ incumbent {bestObj:0.###}.");
                    continue;
                }

                // Check integrality
                int k = FindFractionalIndex(lp.X, cur.Model.IntMask, 1e-6, out double xk);
                if (k < 0)
                {
                    // Integer feasible
                    if (lp.Objective > bestObj + 1e-12)
                    {
                        bestObj = lp.Objective;
                        bestX = lp.X.ToArray();
                        Console.WriteLine($"  New incumbent: z={bestObj:0.###} at [{Str(lp.X)}]");
                    }
                    continue;
                }

                // Branch: x_k ≤ floor(v)  and  x_k ≥ ceil(v)
                double flo = Math.Floor(xk);
                double cei = Math.Ceiling(xk);

                Node left = new Node
                {
                    Model = AddUpperBound(cur.Model, k, flo),
                    Tag = $"x{k + 1} ≤ {flo}"
                };
                Node right = new Node
                {
                    Model = AddLowerBound(cur.Model, k, cei),
                    Tag = $"x{k + 1} ≥ {cei}"
                };

                open.Add(left);
                open.Add(right);

                Console.WriteLine($"  Branching on x{k + 1}={xk:0.###} → [{left.Tag}] and [{right.Tag}]");
                if (!double.IsNegativeInfinity(bestObj))
                {
                    Console.WriteLine($"  Current incumbent: z={bestObj:0.###}");
                }
            }

            Console.WriteLine();
            if (bestX == null)
            {
                Console.WriteLine("No integer-feasible solution found.");
            }
            else
            {
                Console.WriteLine("===== Best Integer Solution =====");
                Console.WriteLine($"z* = {bestObj:0.###}");
                Console.WriteLine($"x* = [{Str(bestX)}]");
            }
        }

        // ---------- Tiny model + parsing ----------

        private enum Sense { Max, Min }
        private enum Rel { LessEqual }
        private enum Sign { Plus }

        private sealed class Model
        {
            public Sense Sense;
            public double[] C;     // n
            public double[,] A;    // m x n
            public double[] B;     // m
            public Rel[] Relations;// m (only <= here)
            public Sign[] Signs;   // n (only + here)
            public bool[] IntMask; // n (true if int/bin)
        }

        private static Model ParseModel(string objLine, string[] constraintLines)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;

            // Objective: "max +2 +3 +3"
            string[] oParts = SplitTokens(objLine);
            if (oParts.Length < 2) throw new Exception("Bad objective line.");
            Sense sense = oParts[0].ToLower().Contains("min") ? Sense.Min : Sense.Max;

            List<double> c = new List<double>();
            for (int i = 1; i < oParts.Length; i++)
                c.Add(ParseSigned(oParts[i], ci));

            // Constraints: all lines except possibly a last "bin/int" line
            List<string> cons = new List<string>(constraintLines);
            bool[] intMask = new bool[c.Count];
            if (cons.Count > 0)
            {
                string last = cons[cons.Count - 1].Trim().ToLower();
                if (last.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).All(t => t == "bin" || t == "int" || t == "cont" || t == "+"))
                {
                    // Treat as type line
                    string[] ts = last.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < Math.Min(ts.Length, intMask.Length); j++)
                        intMask[j] = (ts[j] == "bin" || ts[j] == "int");
                    cons.RemoveAt(cons.Count - 1);
                }
            }

            List<double[]> rows = new List<double[]>();
            List<double> b = new List<double>();
            List<Rel> rels = new List<Rel>();

            foreach (string ln in cons)
            {
                string left, rr, right;
                if (!TrySplitRelation(ln, out left, out rr, out right))
                    throw new Exception("Constraint line missing a relation (<=, >=, =).");

                double rhs = double.Parse(right, ci);

                if (rr == "<=")
                {
                    double[] coeffs = ParseCoeffVector(left, c.Count, ci);
                    rows.Add(coeffs); b.Add(rhs); rels.Add(Rel.LessEqual);
                }
                else if (rr == ">=")
                {
                    double[] coeffs = ParseCoeffVector(left, c.Count, ci);
                    for (int j = 0; j < coeffs.Length; j++) coeffs[j] = -coeffs[j];
                    rows.Add(coeffs); b.Add(-rhs); rels.Add(Rel.LessEqual);
                }
                else if (rr == "=")
                {
                    double[] coeffs = ParseCoeffVector(left, c.Count, ci);
                    rows.Add((double[])coeffs.Clone()); b.Add(rhs); rels.Add(Rel.LessEqual);

                    double[] neg = (double[])coeffs.Clone();
                    for (int j = 0; j < neg.Length; j++) neg[j] = -neg[j];
                    rows.Add(neg); b.Add(-rhs); rels.Add(Rel.LessEqual);
                }
                else
                {
                    throw new Exception("Unsupported relation.");
                }
            }

            double[,] A = new double[rows.Count, c.Count];
            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < c.Count; j++)
                    A[i, j] = rows[i][j];

            Sign[] signs = Enumerable.Repeat(Sign.Plus, c.Count).ToArray();

            return new Model
            {
                Sense = sense,
                C = c.ToArray(),
                A = A,
                B = b.ToArray(),
                Relations = rels.ToArray(),
                Signs = signs,
                IntMask = intMask
            };
        }
        private static bool TrySplitRelation(string line, out string left, out string rel, out string right)
        {
            line = line.Trim();
            // Normalise unicode
            line = line.Replace("≤", "<=").Replace("≥", ">=");
            int pos = line.IndexOf("<=");
            if (pos < 0) pos = line.IndexOf(">=");
            if (pos < 0) pos = line.IndexOf("=");
            if (pos < 0) { left = rel = right = null; return false; }

            string r = line.Substring(pos, (line[pos] == '<' || line[pos] == '>') ? 2 : 1);
            left = line.Substring(0, pos).Trim();
            right = line.Substring(pos + r.Length).Trim();
            rel = r;
            return true;
        }

        private static double[] ParseCoeffVector(string leftSide, int n, System.Globalization.CultureInfo ci)
        {
            string[] toks = leftSide.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (toks.Length != n) throw new Exception("Coefficient count mismatch.");
            double[] row = new double[n];
            for (int j = 0; j < n; j++) row[j] = ParseSigned(toks[j], ci);
            return row;
        }

        private static string[] SplitTokens(string s)
        {
            return s.Replace("≤", "<=").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static double ParseSigned(string token, CultureInfo ci)
        {
            // "+2" "-3" "2" "-4.5"
            if (token.StartsWith("+")) token = token.Substring(1);
            return double.Parse(token, ci);
        }

        // ---------- Branch helpers ----------

        private sealed class Node
        {
            public Model Model;
            public string Tag;
            public double Bound = double.NegativeInfinity;
        }

        private static int PickBest(List<Node> nodes)
        {
            int best = 0;
            double bestVal = nodes[0].Bound;
            for (int i = 1; i < nodes.Count; i++)
            {
                if (nodes[i].Bound > bestVal) { bestVal = nodes[i].Bound; best = i; }
            }
            return best;
        }

        private static int FindFractionalIndex(double[] x, bool[] isInt, double tol, out double value)
        {
            for (int j = 0; j < x.Length; j++)
            {
                if (!isInt[j]) continue;
                double frac = Math.Abs(x[j] - Math.Round(x[j]));
                if (frac > tol) { value = x[j]; return j; }
            }
            value = 0.0;
            return -1;
        }

        private static Model AddUpperBound(Model m, int j, double ub)
        {
            // x_j <= ub  → add row e_j·x <= ub
            int mRows = m.B.Length;
            int n = m.C.Length;

            double[,] A2 = new double[mRows + 1, n];
            for (int i = 0; i < mRows; i++)
                for (int k = 0; k < n; k++)
                    A2[i, k] = m.A[i, k];

            for (int k = 0; k < n; k++) A2[mRows, k] = 0.0;
            A2[mRows, j] = 1.0;

            double[] B2 = new double[mRows + 1];
            Array.Copy(m.B, B2, mRows);
            B2[mRows] = ub;

            Rel[] R2 = new Rel[mRows + 1];
            Array.Copy(m.Relations, R2, mRows);
            R2[mRows] = Rel.LessEqual;

            return new Model { Sense = m.Sense, C = m.C, A = A2, B = B2, Relations = R2, Signs = m.Signs, IntMask = m.IntMask };
        }

        private static Model AddLowerBound(Model m, int j, double lb)
        {
            // x_j >= lb → -x_j <= -lb
            int mRows = m.B.Length;
            int n = m.C.Length;

            double[,] A2 = new double[mRows + 1, n];
            for (int i = 0; i < mRows; i++)
                for (int k = 0; k < n; k++)
                    A2[i, k] = m.A[i, k];

            for (int k = 0; k < n; k++) A2[mRows, k] = 0.0;
            A2[mRows, j] = -1.0;

            double[] B2 = new double[mRows + 1];
            Array.Copy(m.B, B2, mRows);
            B2[mRows] = -lb;

            Rel[] R2 = new Rel[mRows + 1];
            Array.Copy(m.Relations, R2, mRows);
            R2[mRows] = Rel.LessEqual;

            return new Model { Sense = m.Sense, C = m.C, A = A2, B = B2, Relations = R2, Signs = m.Signs, IntMask = m.IntMask };
        }

        private static string Str(double[] v)
        {
            return string.Join(", ", v.Select(z => z.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        // ---------- Tiny LP solver (simplex tableau, <=, x>=0, max) ----------

        private enum LpStatus { Optimal, Unbounded, Infeasible }

        private sealed class LpResult
        {
            public LpStatus Status;
            public double Objective;
            public double[] X;
        }

        private static LpResult SolveLp(Model m)
        {
            // Build standard form: max c^T x,  A x <= b, x >= 0
            int mRows = m.B.Length;
            int nCols = m.C.Length;

            // Tableau dimensions: rows = m + 1, cols = n + m + 1
            // [A | I | b]
            // [-c | 0 | 0]  (objective row)
            double[,] T = new double[mRows + 1, nCols + mRows + 1];

            // A
            for (int i = 0; i < mRows; i++)
                for (int j = 0; j < nCols; j++)
                    T[i, j] = m.A[i, j];

            // Slack I
            for (int i = 0; i < mRows; i++)
                T[i, nCols + i] = 1.0;

            // b
            for (int i = 0; i < mRows; i++)
                T[i, nCols + mRows] = m.B[i];

            // Objective row
            for (int j = 0; j < nCols; j++)
                T[mRows, j] = -m.C[j];

            int[] basis = Enumerable.Range(0, mRows).Select(i => nCols + i).ToArray();

            // Phase II (feasibility assumed since b>=0 typical; if b<0, this will fail as infeasible)
            for (int iter = 0; iter < 1000; iter++)
            {
                // Entering: most positive reduced cost in objective row
                int enter = -1;
                double best = 1e-12;
                for (int j = 0; j < nCols + mRows; j++)
                {
                    double rc = T[mRows, j];
                    if (rc < -1e-12) continue; // we keep objective row as -c; for max, look for positive in -z? Nope:

                    // NOTE: our objective row is -c; during pivoting it becomes reduced costs (negative for improving vars).
                    // To keep it simple: choose the most negative coefficient in objective row.
                }

                // Choose most negative in objective row
                enter = -1;
                double mostNeg = -1e-12;
                for (int j = 0; j < nCols + mRows; j++)
                {
                    if (T[mRows, j] < mostNeg)
                    {
                        mostNeg = T[mRows, j];
                        enter = j;
                    }
                }

                if (enter == -1)
                {
                    // Optimal
                    double[] x = new double[nCols];
                    for (int i = 0; i < mRows; i++)
                    {
                        if (basis[i] < nCols)
                            x[basis[i]] = T[i, nCols + mRows];
                    }
                    double z = T[mRows, nCols + mRows];
                    return new LpResult { Status = LpStatus.Optimal, Objective = z, X = x };
                }

                // Leaving: minimum ratio test
                int leave = -1;
                double bestRatio = double.PositiveInfinity;
                for (int i = 0; i < mRows; i++)
                {
                    double aij = T[i, enter];
                    if (aij > 1e-12)
                    {
                        double ratio = T[i, nCols + mRows] / aij;
                        if (ratio < bestRatio)
                        {
                            bestRatio = ratio;
                            leave = i;
                        }
                    }
                }
                if (leave == -1) return new LpResult { Status = LpStatus.Unbounded, Objective = double.PositiveInfinity, X = new double[nCols] };

                // Pivot (leave row -> normalize; eliminate column elsewhere)
                double piv = T[leave, enter];
                for (int j = 0; j < nCols + mRows + 1; j++) T[leave, j] /= piv;

                for (int i = 0; i <= mRows; i++)
                {
                    if (i == leave) continue;
                    double factor = T[i, enter];
                    if (Math.Abs(factor) < 1e-12) continue;
                    for (int j = 0; j < nCols + mRows + 1; j++)
                        T[i, j] -= factor * T[leave, j];
                }
                basis[leave] = enter;
            }

            return new LpResult { Status = LpStatus.Infeasible, Objective = double.NaN, X = new double[nCols] };
        }
    }
}

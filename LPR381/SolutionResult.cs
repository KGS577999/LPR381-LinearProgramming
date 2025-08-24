using System.Collections.Generic;

namespace LPR381
{
    public class SolutionResult
    {
        public bool IsSolved { get; set; }
        public bool IsOptimal { get; set; }
        public string Message { get; set; }
        public double ObjectiveValue { get; set; }
        public List<double> DecisionVariables { get; set; } = new List<double>();
        
        // Sensitivity Analysis Data - CRITICAL for your module
        public double[,] FinalTableau { get; set; }
        public List<int> Basis { get; set; } = new List<int>();
        public int NumberOfDecisionVariables { get; set; }
        public int NumberOfConstraints { get; set; }
        
        // For tracking iterations in output
        public List<string> Iterations { get; set; } = new List<string>();
    }
}

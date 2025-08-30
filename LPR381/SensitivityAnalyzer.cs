using System;
using System.Collections.Generic;
using System.Linq;

namespace LPR381
{
    public static class SensitivityAnalyzer
    {
        public static void Analyze(SolutionResult solution)
        {
            if (!solution.IsOptimal)
            {
                Console.WriteLine("Cannot perform sensitivity analysis on non-optimal solution.");
                return;
            }

            Console.WriteLine("\n=== SENSITIVITY ANALYSIS MENU ===");
            Console.WriteLine("1. Shadow Prices");
            Console.WriteLine("2. Range of Objective Coefficients (c_j)");
            Console.WriteLine("3. Range of RHS Values (b_i)");
            Console.WriteLine("4. Return to Main Menu");
            Console.Write("Choose an option: ");
            
            var choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    CalculateShadowPrices(solution);
                    break;
                case "2":
                    CalculateObjectiveRanges(solution);
                    break;
                case "3":
                    CalculateRHSRanges(solution);
                    break;
                case "4":
                    return;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }

            // Keep showing the menu until user chooses to return
            if (choice != "4")
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Analyze(solution); // Recursive call to show menu again
            }
        }

        private static void CalculateShadowPrices(SolutionResult solution)
        {
            Console.WriteLine("\n--- Shadow Prices (Dual Variables) ---");
            Console.WriteLine("The shadow price represents the improvement in the");
            Console.WriteLine("objective function per unit increase in RHS value.");
            Console.WriteLine("----------------------------------------");
            
            for (int i = 0; i < solution.NumberOfConstraints; i++)
            {
                int slackIndex = solution.NumberOfDecisionVariables + i;
                double shadowPrice = -solution.FinalTableau[0, slackIndex]; // Negative for max problem
                Console.WriteLine($"Constraint {i + 1}: {shadowPrice:F3}");
            }
        }

        private static void CalculateObjectiveRanges(SolutionResult solution)
        {
            Console.WriteLine("\n--- Objective Coefficient Ranges ---");
            Console.WriteLine("This shows how much each objective coefficient can");
            Console.WriteLine("change without affecting the optimal solution.");
            Console.WriteLine("----------------------------------------");
            
            // Placeholder - we'll implement the actual math in the next phase
            for (int j = 0; j < solution.NumberOfDecisionVariables; j++)
            {
                Console.WriteLine($"Coefficient for x{j + 1}: [TBD, TBD]");
            }
            Console.WriteLine("\n(Implementation in progress - check back soon!)");
        }

        private static void CalculateRHSRanges(SolutionResult solution)
        {
            Console.WriteLine("\n--- RHS Value Ranges ---");
            Console.WriteLine("This shows how much each constraint's RHS value can");
            Console.WriteLine("change without changing the optimal basis.");
            Console.WriteLine("----------------------------------------");
            
            // Placeholder - we'll implement the actual math in the next phase
            for (int i = 0; i < solution.NumberOfConstraints; i++)
            {
                Console.WriteLine($"Constraint {i + 1} RHS: [TBD, TBD]");
            }
            Console.WriteLine("\n(Implementation in progress - check back soon!)");
        }
    }
}

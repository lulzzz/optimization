﻿// Itinero.Optimization - Route optimization for .NET
// Copyright (C) 2016 Abelshausen Ben
// 
// This file is part of Itinero.
// 
// Itinero is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// Itinero is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Itinero. If not, see <http://www.gnu.org/licenses/>.

using Itinero.Optimization.Algorithms.Solvers.Objective;

namespace Itinero.Optimization.Algorithms.Solvers
{
    /// <summary>
    /// An empty operator that does nothing.
    /// </summary>
    public class EmptyOperator<TWeight, TProblem, TObjective, TSolution, TFitness> : IOperator<TWeight, TProblem, TObjective, TSolution, TFitness>
        where TObjective : ObjectiveBase<TProblem, TSolution, TFitness>
        where TWeight : struct
    {
        /// <summary>
        /// Returns the name of the operator.
        /// </summary>
        public string Name
        {
            get { return "EMPTY"; }
        }

        /// <summary>
        /// Returns true if the given objective is supported.
        /// </summary>
        /// <returns></returns>
        public bool Supports(TObjective objective)
        {
            return true;
        }

        /// <summary>
        /// Returns true if there was an improvement, false otherwise.
        /// </summary>
        /// <returns></returns>
        public bool Apply(TProblem problem, TObjective objective, TSolution solution, out TFitness delta)
        {
            delta = objective.Zero;
            return false;
        }
    }
}
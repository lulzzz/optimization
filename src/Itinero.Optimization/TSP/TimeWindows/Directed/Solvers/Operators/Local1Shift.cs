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

using Itinero.Optimization.Algorithms.Directed;
using Itinero.Optimization.Algorithms.Solvers;
using Itinero.Optimization.Algorithms.Solvers.Objective;
using Itinero.Optimization.Tours;
using Itinero.Optimization.Tours.Operations;
using System;
using System.Collections.Generic;

namespace Itinero.Optimization.TSP.TimeWindows.Directed.Solvers.Operators
{
    /// <summary>
    /// A local search procedure to move around and improve the time window 'violations' in a solution.
    /// </summary>
    public class Local1Shift<TObjective> : IOperator<float, TSPTWProblem, TObjective, Tour, float>
        where TObjective : TSPTWObjectiveBase
    {
        private readonly bool _assumeFeasible;

        /// <summary>
        /// Creates a new operator.
        /// </summary>
        public Local1Shift()
        {

        }

        /// <summary>
        /// Creates a new operator.
        /// </summary>
        public Local1Shift(bool assumeFeasible)
        {
            _assumeFeasible = assumeFeasible;
        }

        /// <summary>
        /// Returns the name of the operator.
        /// </summary>
        public string Name
        {
            get { return "LCL_1SHFT_TW"; }
        }

        /// <summary>
        /// Returns true if the given objective is supported.
        /// </summary>
        /// <returns></returns>
        public bool Supports(TObjective objective)
        {
            return true;
        }

        private bool[] _validFlags;

        /// <summary>
        /// Returns true if there was an improvement, false otherwise.
        /// </summary>
        /// <returns></returns>
        public bool Apply(TSPTWProblem problem, TObjective objective, Tour solution, out float delta)
        {
            if (_validFlags == null)
            {
                _validFlags = new bool[problem.Times.Length / 2];
            }

            delta = 0;
            var before = float.MaxValue;
            if (objective.IsNonContinuous)
            {
                before = objective.Calculate(problem, solution);
            }

            // STRATEGY: 
            // 1: try to move a violated customer backwards.
            // 2: try to move a non-violated customer forward.
            // 3: try to move a non-violated customer backward.
            // 4: try to move a violated customer forward.
            if (!_assumeFeasible)
            {
                if (this.MoveViolatedBackward(problem, objective, solution, out delta))
                { // success already, don't try anything else.
                    if (objective.IsNonContinuous)
                    {
                        delta = before - objective.Calculate(problem, solution);
                    }
                    return delta > 0;
                }
            }
            //if (this.MoveNonViolatedForward(problem, objective, solution, out delta))
            //{ // success already, don't try anything else.
            //    if (objective.IsNonContinuous)
            //    {
            //        delta = before - objective.Calculate(problem, solution);
            //    }
            //    return delta > 0;
            //}
            //if (this.MoveNonViolatedBackward(problem, objective, solution, out delta))
            //{ // success already, don't try anything else.
            //    if (objective.IsNonContinuous)
            //    {
            //        delta = before - objective.Calculate(problem, solution);
            //    }
            //    return delta > 0;
            //}
            //if (!_assumeFeasible)
            //{
            //    if (this.MoveViolatedForward(problem, objective, solution, out delta))
            //    { // success already, don't try anything else.
            //        if (objective.IsNonContinuous)
            //        {
            //            delta = before - objective.Calculate(problem, solution);
            //        }
            //        return delta > 0;
            //    }
            //}
            return false;
        }

        /// <summary>
        /// Returns true if there was an improvement, false otherwise.
        /// </summary>
        public bool MoveViolatedBackward(TSPTWProblem problem, TObjective objective, Tour solution, out float delta)
        {
            if (_validFlags == null)
            {
                _validFlags = new bool[problem.Times.Length / 2];
            }

            float time, waitTime, violatedTime;
            int violated;
            var fitness = objective.Calculate(problem, solution, out violated, out violatedTime, out waitTime, out time, ref _validFlags);

            // if no violated customer found return false.
            if (violated == 0)
            {
                delta = 0;
                return false;
            }

            // loop over all customers.
            var enumerator = solution.GetEnumerator();
            var position = 0;
            while (enumerator.MoveNext())
            {
                if (position == 0)
                { // don't move the first customer.
                    position++;
                    continue;
                }

                // get the id of the current customer.
                var current = enumerator.Current;
                var id = DirectedHelper.ExtractId(current);

                // is this customer violated.
                if (_validFlags[id])
                { // no it's not, move on.
                    position++;
                    continue;
                }

                // move over all customers before the current position.
                var enumerator2 = solution.GetEnumerator();
                var position2 = 0;
                while (enumerator2.MoveNext())
                {
                    var current2 = enumerator2.Current;

                    // test and check fitness.
                    var shiftedTour = solution.GetShiftedAfter(current, current2); // generates a tour as if current was placed right after current2.
                    var newFitness = objective.Calculate(problem, shiftedTour);

                    if (newFitness < fitness)
                    { // there is improvement!
                        delta = fitness - newFitness;
                        solution.ShiftAfter(current, current2);
                        return true;
                    }

                    position2++;
                    if (position2 >= position)
                    { // stop, we've reached the customer to place.
                        break;
                    }
                }

                position++;
            }
            delta = 0;
            return false;
        }

        /// <summary>
        /// Returns true if there was an improvement, false otherwise.
        /// </summary>
        /// <returns></returns>
        public bool MoveNonViolatedForward(TSPTWProblem problem, TObjective objective, Tour solution, out float delta)
        {
            // search for invalid customers.
            var enumerator = solution.GetEnumerator();
            var time = 0f;
            var fitness = 0f;
            var position = 0;
            var valids = new List<Tuple<int, int>>(); // al list of customer-position pairs.
            var previous = Constants.NOT_SET;
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                if (previous != Constants.NOT_SET)
                { // keep track of time.
                    time += problem.Times[previous][current];
                }
                var window = problem.Windows[enumerator.Current];
                if (window.Max < time)
                { // ok, unfeasible.
                    fitness += time - window.Max;
                }
                else if (position > 0 && position < problem.Times.Length - 1)
                { // window is valid and customer is not the first 'moveable' customer.
                    if (enumerator.Current != problem.Last)
                    { // when the last customer is fixed, don't try to relocate.
                        valids.Add(new Tuple<int, int>(enumerator.Current, position));
                    }
                }
                if (window.Min > time)
                { // wait here!
                    time = window.Min;
                }

                // increase position.
                position++;
                previous = enumerator.Current;
            }

            // ... ok, if a customer was found, try to move it.
            foreach (var valid in valids)
            {
                // ok try the new position.
                for (var newPosition = valid.Item2 + 1; newPosition < problem.Times.Length; newPosition++)
                {
                    var before = solution.GetCustomerAt(newPosition);

                    if (before == problem.Last)
                    { // cannot move a customer after a fixed last customer.
                        continue;
                    }
                    // calculate new total min diff.
                    var newFitness = 0.0f;
                    previous = Constants.NOT_SET;
                    time = 0;
                    enumerator = solution.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        if (current != valid.Item1)
                        { // ignore invalid, add it after 'before'.
                            if (previous != Constants.NOT_SET)
                            { // keep track if time.
                                time += problem.Times[previous][current];
                            }
                            var window = problem.Windows[enumerator.Current];
                            if (window.Max < time)
                            { // ok, unfeasible and customer is not the first 'moveable' customer.
                                newFitness += time - window.Max;
                                if (_assumeFeasible)
                                {
                                    newFitness = float.MaxValue;
                                    break;
                                }
                            }
                            if (window.Min > time)
                            { // wait here!
                                time = window.Min;
                            }
                            previous = current;
                            if (current == before)
                            { // also add the before->invalid.
                                time += problem.Times[current][valid.Item1];
                                window = problem.Windows[valid.Item1];
                                if (window.Max < time)
                                { // ok, unfeasible and customer is not the first 'moveable' customer.
                                    newFitness += time - window.Max;
                                }
                                if (window.Min > time)
                                { // wait here!
                                    time = window.Min;
                                }
                                previous = valid.Item1;
                            }
                        }
                    }

                    if (newFitness < fitness)
                    { // there is improvement!
                        delta = fitness - newFitness;
                        solution.ShiftAfter(valid.Item1, before);
                        return true;
                    }
                }
            }
            delta = 0;
            return false;
        }

        /// <summary>
        /// Returns true if there was an improvement, false otherwise.
        /// </summary>
        /// <returns></returns>
        public bool MoveNonViolatedBackward(TSPTWProblem problem, TObjective objective, Tour solution, out float delta)
        {
            // search for invalid customers.
            var enumerator = solution.GetEnumerator();
            var time = 0f;
            var fitness = 0f;
            var position = 0;
            var valids = new List<Tuple<int, int>>(); // al list of customer-position pairs.
            var previous = Constants.NOT_SET;
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                if (previous != Constants.NOT_SET)
                { // keep track of time.
                    time += problem.Times[previous][current];
                }
                var window = problem.Windows[enumerator.Current];
                if (window.Max < time)
                { // ok, unfeasible.
                    fitness += time - window.Max;
                }
                else if (position > 1)
                { // window is valid and customer is not the first 'moveable' customer.
                    if (enumerator.Current != problem.Last)
                    { // when the last customer is fixed, don't try to relocate.
                        valids.Add(new Tuple<int, int>(enumerator.Current, position));
                    }
                }
                if (window.Min > time)
                { // wait here!
                    time = window.Min;
                }

                // increase position.
                position++;
                previous = enumerator.Current;
            }

            // ... ok, if a customer was found, try to move it.
            foreach (var valid in valids)
            {
                // ok try the new position.
                for (var newPosition = 1; newPosition < valid.Item2; newPosition++)
                {
                    var before = solution.GetCustomerAt(newPosition - 1);

                    // calculate new total min diff.
                    var newFitness = 0.0f;
                    previous = Constants.NOT_SET;
                    time = 0;
                    enumerator = solution.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        if (current != valid.Item1)
                        { // ignore invalid, add it after 'before'.
                            if (previous != Constants.NOT_SET)
                            { // keep track if time.
                                time += problem.Times[previous][current];
                            }
                            var window = problem.Windows[enumerator.Current];
                            if (window.Max < time)
                            { // ok, unfeasible and customer is not the first 'moveable' customer.
                                newFitness += time - window.Max;
                                if (_assumeFeasible)
                                {
                                    newFitness = float.MaxValue;
                                    break;
                                }
                            }
                            if (window.Min > time)
                            { // wait here!
                                time = window.Min;
                            }
                            previous = current;
                            if (current == before)
                            { // also add the before->invalid.
                                time += problem.Times[current][valid.Item1];
                                window = problem.Windows[valid.Item1];
                                if (window.Max < time)
                                { // ok, unfeasible and customer is not the first 'moveable' customer.
                                    newFitness += time - window.Max;
                                }
                                if (window.Min > time)
                                { // wait here!
                                    time = window.Min;
                                }
                                previous = valid.Item1;
                            }
                        }
                    }

                    if (newFitness < fitness)
                    { // there is improvement!
                        delta = fitness - newFitness;
                        solution.ShiftAfter(valid.Item1, before);
                        return true;
                    }
                }
            }
            delta = 0;
            return false;
        }

        /// <summary>
        /// Returns true if there was an improvement, false otherwise.
        /// </summary>
        /// <returns></returns>
        public bool MoveViolatedForward(TSPTWProblem problem, TObjective objective, Tour solution, out float delta)
        {
            // search for invalid customers.
            var enumerator = solution.GetEnumerator();
            var time = 0f;
            var fitness = 0f;
            var position = 0;
            var invalids = new List<Tuple<int, int>>(); // al list of customer-position pairs.
            var previous = Constants.NOT_SET;
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                if (previous != Constants.NOT_SET)
                { // keep track of time.
                    time += problem.Times[previous][current];
                }
                var window = problem.Windows[enumerator.Current];
                if (window.Max < time && position > 0 && position < problem.Times.Length - 1)
                { // ok, unfeasible and customer is not the first 'moveable' customer.
                    fitness += time - window.Max;
                    if (enumerator.Current != problem.Last)
                    { // when the last customer is fixed, don't try to relocate.
                        invalids.Add(new Tuple<int, int>(enumerator.Current, position));
                    }
                }
                if (window.Min > time)
                { // wait here!
                    time = window.Min;
                }

                // increase position.
                position++;
                previous = enumerator.Current;
            }

            // ... ok, if a customer was found, try to move it.
            foreach (var invalid in invalids)
            {
                // ok try the new position.
                for (var newPosition = invalid.Item2 + 1; newPosition < problem.Times.Length; newPosition++)
                {
                    var before = solution.GetCustomerAt(newPosition);
                    if (before == problem.Last)
                    { // cannot move a customer after a fixed last customer.
                        continue;
                    }

                    // calculate new total min diff.
                    var newFitness = 0.0f;
                    previous = Constants.NOT_SET;
                    time = 0;
                    enumerator = solution.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        if (current != invalid.Item1)
                        { // ignore invalid, add it after 'before'.
                            if (previous != Constants.NOT_SET)
                            { // keep track if time.
                                time += problem.Times[previous][current];
                            }
                            var window = problem.Windows[enumerator.Current];
                            if (window.Max < time)
                            { // ok, unfeasible and customer is not the first 'moveable' customer.
                                newFitness += time - window.Max;
                            }
                            if (window.Min > time)
                            { // wait here!
                                time = window.Min;
                            }
                            previous = current;
                            if (current == before)
                            { // also add the before->invalid.
                                time += problem.Times[current][invalid.Item1];
                                window = problem.Windows[invalid.Item1];
                                if (window.Max < time)
                                { // ok, unfeasible and customer is not the first 'moveable' customer.
                                    newFitness += time - window.Max;
                                }
                                if (window.Min > time)
                                { // wait here!
                                    time = window.Min;
                                }
                                previous = invalid.Item1;
                            }
                        }
                    }

                    if (newFitness < fitness)
                    { // there is improvement!
                        delta = fitness - newFitness;
                        solution.ShiftAfter(invalid.Item1, before);
                        return true;
                    }
                }
            }
            delta = 0;
            return false;
        }

    }
}

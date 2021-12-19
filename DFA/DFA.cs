using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace DFA
{
    /// <summary>
    /// DFA base state
    /// Every state is unique identified by a numeric integer Id
    /// </summary>
    [DataContract]
    public abstract class DFAState : IComparable
    {
        /// <summary>
        /// State ID
        /// </summary>
        [DataMember]
        public int Id { get; set; }
        /// <summary>
        /// Action to execute when in the state
        /// </summary>
        [IgnoreDataMember]
        public Action Action { get; set; }

        public int CompareTo(object obj)
        {
            if (Id < ((DFAState)obj).Id)
            {
                return -1;
            }
            else if (Id > ((DFAState)obj).Id)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    /// <summary>
    /// State comparer
    /// Two states are the same when their ids are equals
    /// </summary>
    internal class DFAStateComparer : IComparer<DFAState>
    {
        public int Compare(DFAState x, DFAState y)
        {
            if (x.Id < y.Id)
            {
                return -1;
            }
            else if (x.Id > y.Id)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Serialization format
    /// </summary>
    public enum DFASerializationFormat { XML, JSON }

    /// <summary>
    /// DFA
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    [DataContract]
    public abstract class DFA<TState> where TState : DFAState
    {
        [IgnoreDataMember]
        private Dictionary<(int From, int To), Func<bool>> transitionTable = new Dictionary<(int From, int To), Func<bool>>();
        [IgnoreDataMember]
        private Dictionary<int, TState> statesById = new Dictionary<int, TState>();
        [DataMember]
        private SortedSet<TState> states = new SortedSet<TState>(new DFAStateComparer());
        [DataMember]
        private int currentStateId;

        public IEnumerable<TState> States { get => states.AsEnumerable(); }
        public TState CurrentState { get => statesById[currentStateId]; set => statesById[currentStateId] = value; }
        public int CurrentStateId { get => currentStateId; set => currentStateId = value; }

        public DFA()
        {
            CurrentState = null;
            this.DefineStates();
            this.DefineTransitions();
        }

        /// <summary>
        /// BUild states
        /// </summary>
        public abstract void DefineStates();

        /// <summary>
        /// Build transition table
        /// </summary>
        public abstract void DefineTransitions();

        #region Definition

        public void AddState(int stateId, TState state)
        {
            statesById[stateId] = state;
            state.Id = stateId;
            states.Add(state);
        }

        public void AddState(TState state)
        {
            int stateId = state.Id;
            statesById[stateId] = state;
            states.Add(state);
        }

        public void AddTransitionLink(int from, int to, Func<bool> checkFunction)
        {
            transitionTable[(from, to)] = checkFunction;
        }

        public void AddTransitionLink(TState from, TState to, Func<bool> checkFunction)
        {
            transitionTable[(from.Id, to.Id)] = checkFunction;
        }

        #endregion

        #region Execution

        public void StartFrom(int fromId)
        {
            currentStateId = fromId;
        }

        public void StartFrom(TState from)
        {
            currentStateId = from.Id;
        }

        public bool CanMoveTo(TState to) => CanMoveTo(to.Id);

        public bool CanMoveTo(int toId)
        {
            if (transitionTable.ContainsKey((currentStateId, toId)))
            {
                if (transitionTable[(currentStateId, toId)].Invoke())
                {
                    return true;
                }
            }
            return false;
        }

        public bool MoveTo(TState to) => MoveTo(to.Id);

        public bool MoveTo(int toId)
        {
            if (transitionTable.ContainsKey((currentStateId, toId)))
            {
                if (transitionTable[(currentStateId, toId)].Invoke())
                {
                    currentStateId = toId;
                    CurrentState.Action.Invoke();
                    return true;
                }
            }
            return false;
        }

        public IEnumerable<TState> GetNextStatesEx()
        {
            List<TState> nextStates = new List<TState>();
            foreach ((int From, int To) k in transitionTable.Keys)
            {
                if (k.From == currentStateId)
                {
                    nextStates.Add(statesById[k.To]);
                }
            }
            return nextStates;
        }


        public IEnumerable<int> GetNextStates()
        {
            List<int> nextStates = new List<int>();
            foreach (var k in transitionTable.Keys)
            {
                if (k.From == currentStateId)
                {
                    nextStates.Add(k.To);
                }
            }
            return nextStates;
        }

        public bool Move()
        {
            IEnumerable<int> nexts = GetNextStates();
            if (!nexts.Any())
            {
                return false;
            }
            foreach (int next in nexts)
            {
                if (CanMoveTo(next))
                {
                    MoveTo(next);
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Save/Load Memory

        public void Save(Stream output, DFASerializationFormat format = DFASerializationFormat.XML)
        {
            switch (format)
            {
                case DFASerializationFormat.XML:                    ;
                    DataContractSerializer serializer = new DataContractSerializer(GetType());
                    serializer.WriteObject(output, this);
                    break;
                case DFASerializationFormat.JSON:
                    throw new NotImplementedException($"Format {format} not yet implemented");
                default:
                    throw new SerializationException($"Wrong format {format}");
            }
        }

        public static TDFA Load<TDFA>(Stream input, DFASerializationFormat format = DFASerializationFormat.XML) where TDFA : DFA<TState>, new()
        {
            switch (format)
            {
                case DFASerializationFormat.XML:
                    DataContractSerializer serializer = new DataContractSerializer(typeof(TDFA));
                    TDFA dfa = (TDFA)serializer.ReadObject(input);
                    TDFA tempDfa = new TDFA();
                    foreach (TState st in dfa.States)
                    {
                        st.Action = tempDfa.States.Where(tempSt => tempSt.Id == st.Id).First().Action;
                    }
                    if (dfa.States.Any(st => st.Id == dfa.CurrentState.Id))
                    {
                        dfa.CurrentState = dfa.States.Where(st => st.Id == dfa.CurrentState.Id).First();
                    }
                    return dfa;
                case DFASerializationFormat.JSON:
                    throw new NotImplementedException($"Format {format} not yet implemented");
                default:
                    throw new SerializationException($"Wrong format {format}");
            }
        }

        #endregion
    }
}
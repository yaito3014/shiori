using System.Linq;
using NUnit.Framework;

namespace Shiori.Tests
{
    public class AnimatorYamlTests
    {
        // Written by hand in the shape Unity saves: negative fileIDs, empty m_Name on transitions,
        // {} and [] collections, and lists of bare references.
        private const string Controller = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1101 &-1101001
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_Name:
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Hat
    m_EventTreshold: 0
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: 1102002}
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.25
  m_HasExitTime: 0
--- !u!1101 &1101002
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_Name:
  m_Conditions:
  - m_ConditionMode: 2
    m_ConditionEvent: Hat
    m_EventTreshold: 0
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: 0}
  m_IsExit: 1
  serializedVersion: 3
  m_TransitionDuration: 0
  m_HasExitTime: 0
--- !u!1101 &1101003
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_Name:
  m_Conditions:
  - m_ConditionMode: 3
    m_ConditionEvent: Speed
    m_EventTreshold: 0.5
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: -1102001}
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0
  m_HasExitTime: 0
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_Name: FX
  serializedVersion: 5
  m_AnimatorParameters:
  - m_Name: Hat
    m_Type: 4
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
  - m_Name: Speed
    m_Type: 1
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {fileID: 9100000}
  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {fileID: 1107001}
    m_Mask: {fileID: 0}
    m_DefaultWeight: 0
    m_Controller: {fileID: 9100000}
  - serializedVersion: 5
    m_Name: Face
    m_StateMachine: {fileID: 1107002}
    m_Mask: {fileID: 0}
    m_DefaultWeight: 1
    m_Controller: {fileID: 9100000}
--- !u!1102 &-1102001
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_Name: Idle
  m_Speed: 1
  m_Transitions:
  - {fileID: -1101001}
  m_StateMachineBehaviours: []
  m_Position: {x: 50, y: 50, z: 0}
  m_WriteDefaultValues: 0
  m_Motion: {fileID: 7400000, guid: aaaa1111aaaa1111aaaa1111aaaa1111, type: 2}
  m_Tag:
--- !u!1102 &1102002
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_Name: Hat On
  m_Speed: 1
  m_Transitions:
  - {fileID: 1101002}
  m_StateMachineBehaviours:
  - {fileID: 11400001}
  m_Position: {x: 50, y: 50, z: 0}
  m_WriteDefaultValues: 0
  m_Motion: {fileID: 7400000, guid: bbbb2222bbbb2222bbbb2222bbbb2222, type: 2}
  m_Tag:
--- !u!114 &11400001
MonoBehaviour:
  m_ObjectHideFlags: 1
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: cccc3333cccc3333cccc3333cccc3333, type: 3}
  m_Name:
  localOnly: 1
--- !u!1107 &1107001
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_Name: Base Layer
  m_ChildStates:
  - serializedVersion: 1
    m_State: {fileID: -1102001}
    m_Position: {x: 300, y: 0, z: 0}
  - serializedVersion: 1
    m_State: {fileID: 1102002}
    m_Position: {x: 300, y: 100, z: 0}
  m_ChildStateMachines: []
  m_AnyStateTransitions:
  - {fileID: 1101003}
  m_EntryTransitions: []
  m_StateMachineTransitions: {}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {x: 50, y: 20, z: 0}
  m_EntryPosition: {x: 50, y: 120, z: 0}
  m_ExitPosition: {x: 800, y: 120, z: 0}
  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}
  m_DefaultState: {fileID: -1102001}
--- !u!1107 &1107002
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_Name: Face
  m_ChildStates: []
  m_ChildStateMachines: []
  m_AnyStateTransitions: []
  m_EntryTransitions: []
  m_StateMachineTransitions: {}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {x: 50, y: 20, z: 0}
  m_EntryPosition: {x: 50, y: 120, z: 0}
  m_ExitPosition: {x: 800, y: 120, z: 0}
  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}
  m_DefaultState: {fileID: 0}
";

        private const string NewState = @"--- !u!1102 &1102003
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_Name: Wave
  m_Speed: 1
  m_Transitions: []
  m_StateMachineBehaviours: []
  m_Position: {x: 50, y: 50, z: 0}
  m_WriteDefaultValues: 0
  m_Motion: {fileID: 0}
  m_Tag:
";

        // A second Idle -> Hat On transition, so two transitions share both ends.
        private const string SecondIdleTransition = @"--- !u!1101 &1101004
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_Name:
  m_Conditions:
  - m_ConditionMode: 3
    m_ConditionEvent: Speed
    m_EventTreshold: 0.5
  m_DstStateMachine: {fileID: 0}
  m_DstState: {fileID: 1102002}
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0
  m_HasExitTime: 0
";

        private const string SecondBehaviour = @"--- !u!114 &11400002
MonoBehaviour:
  m_ObjectHideFlags: 1
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: eeee5555eeee5555eeee5555eeee5555, type: 3}
  m_Name:
";

        private sealed class Resolver : IGuidResolver
        {
            public string Resolve(string guid)
            {
                switch (guid)
                {
                    case "aaaa1111aaaa1111aaaa1111aaaa1111": return "Assets/FX/Idle.anim";
                    case "bbbb2222bbbb2222bbbb2222bbbb2222": return "Assets/FX/HatOn.anim";
                    case "dddd4444dddd4444dddd4444dddd4444": return "Assets/FX/HatOn2.anim";
                    case "cccc3333cccc3333cccc3333cccc3333": return "Packages/com.vrchat.avatars/AvatarParameterDriver.cs";
                    case "eeee5555eeee5555eeee5555eeee5555": return "Packages/com.vrchat.avatars/TrackingControl.cs";
                    default: return null;
                }
            }
        }

        private static readonly Resolver Names = new Resolver();

        [Test]
        public void Parse_ReadsNegativeIdsEmptyNamesAndEmptyCollections()
        {
            var docs = UnityYamlParser.Parse(Controller);
            Assert.That(docs[0].FileId, Is.EqualTo(-1101001));
            Assert.That(docs[0].Body["m_Name"].Kind, Is.EqualTo(YamlNodeKind.Scalar));
            Assert.That(docs[0].Body["m_Name"].Value, Is.Empty);
            Assert.That(docs[0].Body["m_Conditions"].Items.Single()["m_ConditionEvent"].Value, Is.EqualTo("Hat"));
            var machine = docs.Single(d => d.FileId == 1107001).Body;
            Assert.That(machine["m_StateMachineTransitions"].Kind, Is.EqualTo(YamlNodeKind.Map));
            Assert.That(machine["m_ChildStateMachines"].Kind, Is.EqualTo(YamlNodeKind.Sequence));
            Assert.That(machine["m_AnyStateTransitions"].Items.Single().AsReference().FileId, Is.EqualTo(1101003));
        }

        [Test]
        public void Diff_IdenticalControllersHaveNoChanges()
        {
            Assert.That(UnityYamlDiff.Compare(Controller, Controller, Names), Is.Empty);
        }

        [Test]
        public void Diff_ParameterAddedInTheMiddle_IsReportedByNameAlone()
        {
            var changed = Controller.Replace("  - m_Name: Speed\n", "  - m_Name: Wave\n    m_Type: 9\n    m_DefaultFloat: 0\n    m_DefaultInt: 0\n    m_DefaultBool: 0\n    m_Controller: {fileID: 9100000}\n  - m_Name: Speed\n");

            var change = UnityYamlDiff.Compare(Controller, changed, Names).Single();

            Assert.That(change.TypeName, Is.EqualTo("AnimatorController"));
            Assert.That(change.Location, Is.EqualTo("FX"));
            Assert.That(change.Properties.Select(p => p.Path), Is.All.StartsWith("m_AnimatorParameters[Wave]."), "Speed did not move to another index");
            Assert.That(change.Properties.All(p => p.OldValue == null), Is.True);
            Assert.That(change.Properties.Single(p => p.Path == "m_AnimatorParameters[Wave].m_Type").NewValue, Is.EqualTo("Trigger"));
        }

        [Test]
        public void Diff_ParameterTypeAndDefaultAreShownByName()
        {
            var changed = Controller.Replace("  - m_Name: Speed\n    m_Type: 1\n    m_DefaultFloat: 0\n", "  - m_Name: Speed\n    m_Type: 3\n    m_DefaultFloat: 0.5\n");

            var change = UnityYamlDiff.Compare(Controller, changed, Names).Single();

            var byPath = change.Properties.ToDictionary(p => p.Path);
            Assert.That(byPath.Keys, Is.EquivalentTo(new[] { "m_AnimatorParameters[Speed].m_Type", "m_AnimatorParameters[Speed].m_DefaultFloat" }));
            Assert.That(byPath["m_AnimatorParameters[Speed].m_Type"].OldValue, Is.EqualTo("Float"));
            Assert.That(byPath["m_AnimatorParameters[Speed].m_Type"].NewValue, Is.EqualTo("Int"));
        }

        [Test]
        public void Diff_LayerOrderIsReported_BecauseLowerLayersOverrideHigherOnes()
        {
            var baseLayer = "  - serializedVersion: 5\n    m_Name: Base Layer\n    m_StateMachine: {fileID: 1107001}\n    m_Mask: {fileID: 0}\n    m_DefaultWeight: 0\n    m_Controller: {fileID: 9100000}\n";
            var face = "  - serializedVersion: 5\n    m_Name: Face\n    m_StateMachine: {fileID: 1107002}\n    m_Mask: {fileID: 0}\n    m_DefaultWeight: 1\n    m_Controller: {fileID: 9100000}\n";
            Assert.That(Controller, Does.Contain(baseLayer + face));
            var swapped = Controller.Replace(baseLayer + face, face + baseLayer);

            var change = UnityYamlDiff.Compare(Controller, swapped, Names).Single();

            var order = change.Properties.Single();
            Assert.That(order.Path, Is.EqualTo("m_AnimatorLayers"));
            Assert.That(order.OldValue, Is.EqualTo("[Base Layer, Face]"));
            Assert.That(order.NewValue, Is.EqualTo("[Face, Base Layer]"));
        }

        [Test]
        public void Diff_RenamedLayer_IsOneNameChange_BecauseLayersAreMatchedByTheirStateMachine()
        {
            var renamed = Controller.Replace("    m_Name: Face\n", "    m_Name: Expressions\n");

            var change = UnityYamlDiff.Compare(Controller, renamed, Names).Single();

            var property = change.Properties.Single();
            Assert.That(property.Path, Is.EqualTo("m_AnimatorLayers[Expressions].m_Name"));
            Assert.That(property.OldValue, Is.EqualTo("Face"));
            Assert.That(property.NewValue, Is.EqualTo("Expressions"));
        }

        [Test]
        public void Diff_LayersWithoutAStateMachine_AreMatchedByName()
        {
            var synced = Controller.Replace("    m_StateMachine: {fileID: 1107002}\n", "    m_StateMachine: {fileID: 0}\n");
            var changed = synced.Replace("    m_DefaultWeight: 1\n", "    m_DefaultWeight: 0.5\n");

            var property = UnityYamlDiff.Compare(synced, changed, Names).Single().Properties.Single();

            Assert.That(property.Path, Is.EqualTo("m_AnimatorLayers[Face].m_DefaultWeight"));
        }

        [Test]
        public void Diff_StatesAreNamedWithTheirStateMachine_AndMotionsByAsset()
        {
            var changed = Controller.Replace("guid: bbbb2222bbbb2222bbbb2222bbbb2222", "guid: dddd4444dddd4444dddd4444dddd4444")
                .Replace("  m_Name: Hat On\n  m_Speed: 1\n", "  m_Name: Hat On\n  m_Speed: 2\n");

            var change = UnityYamlDiff.Compare(Controller, changed, Names).Single();

            Assert.That(change.TypeName, Is.EqualTo("AnimatorState"));
            Assert.That(change.Location, Is.EqualTo("Base Layer/Hat On"));
            var byPath = change.Properties.ToDictionary(p => p.Path);
            Assert.That(byPath.Keys, Is.EquivalentTo(new[] { "m_Speed", "m_Motion" }));
            Assert.That(byPath["m_Motion"].OldValue, Is.EqualTo("Assets/FX/HatOn.anim"));
            Assert.That(byPath["m_Motion"].NewValue, Is.EqualTo("Assets/FX/HatOn2.anim"));
        }

        [Test]
        public void Diff_TransitionsAreNamedFromSourceToDestination()
        {
            var changed = Controller.Replace("  - m_ConditionMode: 1\n    m_ConditionEvent: Hat\n", "  - m_ConditionMode: 2\n    m_ConditionEvent: Hat\n")
                .Replace("    m_EventTreshold: 0.5\n", "    m_EventTreshold: 0.75\n")
                .Replace("  m_IsExit: 1\n  serializedVersion: 3\n  m_TransitionDuration: 0\n", "  m_IsExit: 1\n  serializedVersion: 3\n  m_TransitionDuration: 0.1\n");

            var changes = UnityYamlDiff.Compare(Controller, changed, Names);

            Assert.That(changes.Select(c => c.Location), Is.EquivalentTo(new[]
            {
                "Base Layer/Idle → Base Layer/Hat On",
                "Base Layer/Hat On → Exit",
                "Base Layer/Any State → Base Layer/Idle",
            }));
            var toHat = changes.Single(c => c.FileId == -1101001).Properties.Single();
            Assert.That(toHat.Path, Is.EqualTo("m_Conditions[0].m_ConditionMode"));
            Assert.That(toHat.OldValue, Is.EqualTo("If"));
            Assert.That(toHat.NewValue, Is.EqualTo("IfNot"));
            Assert.That(changes.Single(c => c.FileId == 1101003).Properties.Single().Path, Is.EqualTo("m_Conditions[0].m_EventTreshold"));
        }

        [Test]
        public void Diff_TransitionsLeavingASubStateMachine_StartAtThatMachine()
        {
            var withSub = Controller.Replace(
                    "  m_Name: Face\n  m_ChildStates: []\n  m_ChildStateMachines: []\n  m_AnyStateTransitions: []\n  m_EntryTransitions: []\n  m_StateMachineTransitions: {}\n",
                    "  m_Name: Face\n  m_ChildStates: []\n  m_ChildStateMachines:\n  - serializedVersion: 1\n    m_StateMachine: {fileID: 1107003}\n    m_Position: {x: 0, y: 0, z: 0}\n  m_AnyStateTransitions: []\n  m_EntryTransitions: []\n  m_StateMachineTransitions:\n  - first: {fileID: 1107003}\n    second:\n    - {fileID: 1109001}\n")
                + "--- !u!1107 &1107003\nAnimatorStateMachine:\n  m_Name: Blink\n  m_ChildStates: []\n  m_ChildStateMachines: []\n  m_StateMachineTransitions: {}\n"
                + "--- !u!1109 &1109001\nAnimatorTransition:\n  m_Name: \n  m_Conditions: []\n  m_DstStateMachine: {fileID: 0}\n  m_DstState: {fileID: 0}\n  m_IsExit: 1\n";
            var changed = withSub.Replace("  m_Conditions: []\n  m_DstStateMachine: {fileID: 0}\n  m_DstState: {fileID: 0}\n  m_IsExit: 1\n", "  m_Conditions: []\n  m_DstStateMachine: {fileID: 0}\n  m_DstState: {fileID: 0}\n  m_IsExit: 0\n");

            var change = UnityYamlDiff.Compare(withSub, changed, Names).Single();

            Assert.That(change.TypeName, Is.EqualTo("AnimatorTransition"));
            Assert.That(change.Location, Is.EqualTo("Face/Blink"), "no destination after the change, so only the source");
            Assert.That(UnityYamlDiff.Compare(changed, withSub, Names).Single().Location, Is.EqualTo("Face/Blink → Exit"));
        }

        [Test]
        public void Diff_ReferencesToStatesNameTheState()
        {
            var changed = Controller.Replace("  m_DefaultState: {fileID: -1102001}", "  m_DefaultState: {fileID: 1102002}");

            var property = UnityYamlDiff.Compare(Controller, changed, Names).Single().Properties.Single();

            Assert.That(property.Path, Is.EqualTo("m_DefaultState"));
            Assert.That(property.OldValue, Is.EqualTo("AnimatorState (Base Layer/Idle)"));
            Assert.That(property.NewValue, Is.EqualTo("AnimatorState (Base Layer/Hat On)"));
        }

        [Test]
        public void Diff_MovingNodesInTheGraphIsNotAChange()
        {
            var moved = Controller.Replace("    m_Position: {x: 300, y: 100, z: 0}", "    m_Position: {x: 420, y: 180, z: 0}")
                .Replace("  m_AnyStatePosition: {x: 50, y: 20, z: 0}", "  m_AnyStatePosition: {x: 60, y: 30, z: 0}")
                .Replace("  m_EntryPosition: {x: 50, y: 120, z: 0}", "  m_EntryPosition: {x: 10, y: 10, z: 0}")
                .Replace("  m_ExitPosition: {x: 800, y: 120, z: 0}", "  m_ExitPosition: {x: 900, y: 120, z: 0}")
                .Replace("  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}", "  m_ParentStateMachinePosition: {x: 900, y: 20, z: 0}")
                .Replace("  m_Position: {x: 50, y: 50, z: 0}", "  m_Position: {x: 0, y: 0, z: 0}");
            Assert.That(moved, Is.Not.EqualTo(Controller));

            Assert.That(UnityYamlDiff.Compare(Controller, moved, Names), Is.Empty);
        }

        [Test]
        public void Diff_StateAddedInTheMiddle_IsReportedAloneInItsStateMachine()
        {
            var changed = Controller.Replace(
                    "  - serializedVersion: 1\n    m_State: {fileID: 1102002}\n",
                    "  - serializedVersion: 1\n    m_State: {fileID: 1102003}\n    m_Position: {x: 600, y: 0, z: 0}\n  - serializedVersion: 1\n    m_State: {fileID: 1102002}\n")
                + NewState;

            var changes = UnityYamlDiff.Compare(Controller, changed, Names);

            Assert.That(changes.Count, Is.EqualTo(2));
            var added = changes.Single(c => c.Kind == UnityObjectChangeKind.Added);
            Assert.That(added.Location, Is.EqualTo("Base Layer/Wave"));
            var machine = changes.Single(c => c.FileId == 1107001);
            Assert.That(machine.Location, Is.EqualTo("Base Layer"));
            var childState = machine.Properties.Single();
            Assert.That(childState.Path, Is.EqualTo("m_ChildStates[Base Layer/Wave].m_State"));
            Assert.That(childState.OldValue, Is.Null);
            Assert.That(childState.NewValue, Is.EqualTo("AnimatorState (Base Layer/Wave)"));
        }

        [Test]
        public void Diff_TransitionAddedAndReordered_AreBothVisibleOnTheState()
        {
            var added = Controller.Replace("  m_Transitions:\n  - {fileID: -1101001}\n", "  m_Transitions:\n  - {fileID: -1101001}\n  - {fileID: 1101003}\n");
            var reordered = Controller.Replace("  m_Transitions:\n  - {fileID: -1101001}\n", "  m_Transitions:\n  - {fileID: 1101003}\n  - {fileID: -1101001}\n");

            var addedRow = UnityYamlDiff.Compare(Controller, added, Names).Single(c => c.FileId == -1102001).Properties.Single();
            Assert.That(addedRow.Path, Is.EqualTo("m_Transitions"));
            Assert.That(addedRow.OldValue, Is.Null);
            Assert.That(addedRow.NewValue, Does.StartWith("AnimatorStateTransition ("));

            var orderRows = UnityYamlDiff.Compare(added, reordered, Names).Single(c => c.FileId == -1102001).Properties;
            Assert.That(orderRows.Single().Path, Is.EqualTo("m_Transitions"), "the order of a state's transitions is their priority");
        }

        [Test]
        public void Diff_StateBehavioursAreLocatedAtTheirState()
        {
            var changed = Controller.Replace("  localOnly: 1\n", "  localOnly: 0\n");

            var change = UnityYamlDiff.Compare(Controller, changed, Names).Single();

            Assert.That(change.TypeName, Is.EqualTo("MonoBehaviour"));
            Assert.That(change.Location, Is.EqualTo("Base Layer/Hat On"));
            Assert.That(change.Properties.Single().Path, Is.EqualTo("localOnly"));
        }

        private static UnityPropertyChange IdleTransitionOrder(string secondTransition)
        {
            var both = Controller + secondTransition;
            var first = both.Replace("  m_Transitions:\n  - {fileID: -1101001}\n", "  m_Transitions:\n  - {fileID: -1101001}\n  - {fileID: 1101004}\n");
            var swapped = both.Replace("  m_Transitions:\n  - {fileID: -1101001}\n", "  m_Transitions:\n  - {fileID: 1101004}\n  - {fileID: -1101001}\n");
            return UnityYamlDiff.Compare(first, swapped, Names).Single(c => c.FileId == -1102001).Properties.Single();
        }

        [Test]
        public void Diff_ReorderedTransitionsBetweenTheSameStates_AreToldApartByTheirConditions()
        {
            var row = IdleTransitionOrder(SecondIdleTransition);

            Assert.That(row.Path, Is.EqualTo("m_Transitions"));
            Assert.That(row.OldValue, Is.EqualTo("[Base Layer/Idle → Base Layer/Hat On (Hat If), Base Layer/Idle → Base Layer/Hat On (Speed Greater 0.5)]"));
            Assert.That(row.NewValue, Is.EqualTo("[Base Layer/Idle → Base Layer/Hat On (Speed Greater 0.5), Base Layer/Idle → Base Layer/Hat On (Hat If)]"));
        }

        [Test]
        public void Diff_ReorderedTransitionsWithTheSameConditions_AreNumberedInTheirOldOrder()
        {
            var sameConditions = SecondIdleTransition.Replace("  - m_ConditionMode: 3\n    m_ConditionEvent: Speed\n    m_EventTreshold: 0.5\n", "  - m_ConditionMode: 1\n    m_ConditionEvent: Hat\n    m_EventTreshold: 0\n");

            var row = IdleTransitionOrder(sameConditions);

            Assert.That(row.OldValue, Is.EqualTo("[Base Layer/Idle → Base Layer/Hat On (Hat If) (1), Base Layer/Idle → Base Layer/Hat On (Hat If) (2)]"));
            Assert.That(row.NewValue, Is.EqualTo("[Base Layer/Idle → Base Layer/Hat On (Hat If) (2), Base Layer/Idle → Base Layer/Hat On (Hat If) (1)]"));
        }

        [Test]
        public void Diff_ReorderedBehavioursOnOneState_AreToldApartByTheirScript()
        {
            var both = Controller + SecondBehaviour;
            var first = both.Replace("  m_StateMachineBehaviours:\n  - {fileID: 11400001}\n", "  m_StateMachineBehaviours:\n  - {fileID: 11400001}\n  - {fileID: 11400002}\n");
            var swapped = both.Replace("  m_StateMachineBehaviours:\n  - {fileID: 11400001}\n", "  m_StateMachineBehaviours:\n  - {fileID: 11400002}\n  - {fileID: 11400001}\n");

            var row = UnityYamlDiff.Compare(first, swapped, Names).Single(c => c.FileId == 1102002).Properties.Single();

            Assert.That(row.Path, Is.EqualTo("m_StateMachineBehaviours"));
            Assert.That(row.OldValue, Is.EqualTo("[Base Layer/Hat On (AvatarParameterDriver), Base Layer/Hat On (TrackingControl)]"));
            Assert.That(row.NewValue, Is.EqualTo("[Base Layer/Hat On (TrackingControl), Base Layer/Hat On (AvatarParameterDriver)]"));
        }

        [Test]
        public void Diff_AnimatorRulesDoNotApplyToOtherObjects()
        {
            const string prefab = @"--- !u!1 &100
GameObject:
  m_Component:
  - component: {fileID: 11400000}
  m_Name: Root
--- !u!114 &11400000
MonoBehaviour:
  m_GameObject: {fileID: 100}
  m_Position: {x: 0, y: 0, z: 0}
  m_Transitions:
  - {fileID: 100}
  m_Type: 1
";
            var changed = prefab.Replace("m_Position: {x: 0, y: 0, z: 0}", "m_Position: {x: 1, y: 0, z: 0}")
                .Replace("  m_Transitions:\n  - {fileID: 100}\n", "  m_Transitions:\n  - {fileID: 11400000}\n  - {fileID: 100}\n")
                .Replace("m_Type: 1", "m_Type: 3");

            var change = UnityYamlDiff.Compare(prefab, changed, Names).Single();

            Assert.That(change.Properties.Select(p => p.Path), Is.EquivalentTo(new[] { "m_Position.x", "m_Transitions[0]", "m_Transitions[1]", "m_Type" }));
            Assert.That(change.Properties.Single(p => p.Path == "m_Type").NewValue, Is.EqualTo("3"), "enum names are only for animator fields");
        }
    }
}

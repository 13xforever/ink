using System;
using Ink;
using Ink.Runtime;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Path = Ink.Runtime.Path;

namespace Tests
{
    public enum TestMode
    {
        Normal,
        JsonRoundTrip
    }

    [TestFixture(TestMode.Normal)]
    [TestFixture(TestMode.JsonRoundTrip)]
    public class Tests
    {
        
        private TestMode _mode;

        private bool _testingErrors;
        private List<string> _errorMessages = new List<string> ();
        private List<string> _warningMessages = new List<string>();
        private List<string> _authorMessages = new List<string>();


        public Tests (TestMode mode)
        {
            _mode = mode;
        }

        [Test()]
        public void TestArgumentNameCollisions()
        {
            CompileStringWithoutRuntime(@"
VAR global_var = 5

~ pass_divert(-> knot_name)
{variable_param_test(10)}

=== function aTarget() ===
   ~ return true

=== function pass_divert(aTarget) ===
    Should be a divert target, but is a read count:- {aTarget}

=== function variable_param_test(global_var) ===
    ~ return global_var

=== knot_name ===
    -> END
", testingErrors: true);

            Assert.That(_errorMessages.Count, Is.EqualTo(2));
            Assert.That(HadError("name has already been used for a function"), Is.True);
            Assert.That(HadError("name has already been used for a var"), Is.True);
        }

        [Test()]
        public void TestArgumentShouldntConflictWithGatherElsewhere()
        {
            // Testing that there are no errors only
            CompileStringWithoutRuntime(@"
== knot ==
- (x) -> DONE

== function f(x) ==
Nothing
");
        }

        [Test()]
        public void TestArithmetic()
        {
            Story story = CompileString(@"
{ 2 * 3 + 5 * 6 }
{8 mod 3}
{13 % 5}
{ 7 / 3 }
{ 7 / 3.0 }
{ 10 - 2 }
{ 2 * (5-1) }
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("36\n2\n3\n2\n2" + System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator + "3333333\n8\n8\n"));
        }

        [Test()]
        public void TestBasicStringLiterals()
        {
            var story = CompileString(@"
VAR x = ""Hello world 1""
{x}
Hello {""world""} 2.
");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Hello world 1\nHello world 2.\n"));
        }

        [Test()]
        public void TestBasicTunnel()
        {
            Story story = CompileString(@"
-> f ->
<> world

== f ==
Hello
->->
");

            Assert.That(story.Continue(), Is.EqualTo("Hello world\n"));
        }

        [Test()]
        public void TestBlanksInInlineSequences()
        {
            var story = CompileString(@"
1. -> seq1 ->
2. -> seq1 ->
3. -> seq1 ->
4. -> seq1 ->
\---
1. -> seq2 ->
2. -> seq2 ->
3. -> seq2 ->
\---
1. -> seq3 ->
2. -> seq3 ->
3. -> seq3 ->
\---
1. -> seq4 ->
2. -> seq4 ->
3. -> seq4 ->

== seq1 ==
{a||b}
->->

== seq2 ==
{|a}
->->

== seq3 ==
{a|}
->->

== seq4 ==
{|}
->->".Replace("\r", ""));

            Assert.That(
story.ContinueMaximally().Replace("\r", ""), Is.EqualTo(@"1. a
2.
3. b
4. b
---
1.
2. a
3. a
---
1. a
2.
3.
---
1.
2.
3.
".Replace("\r", "")));
        }

        [Test()]
        public void TestAllSequenceTypes()
        {
            var storyStr =
                @"
~ SEED_RANDOM(1)

Once: {f_once()} {f_once()} {f_once()} {f_once()}
Stopping: {f_stopping()} {f_stopping()} {f_stopping()} {f_stopping()}
Default: {f_default()} {f_default()} {f_default()} {f_default()}
Cycle: {f_cycle()} {f_cycle()} {f_cycle()} {f_cycle()}
Shuffle: {f_shuffle()} {f_shuffle()} {f_shuffle()} {f_shuffle()}
Shuffle stopping: {f_shuffle_stopping()} {f_shuffle_stopping()} {f_shuffle_stopping()} {f_shuffle_stopping()}
Shuffle once: {f_shuffle_once()} {f_shuffle_once()} {f_shuffle_once()} {f_shuffle_once()}

== function f_once ==
{once:
    - one
    - two
}

== function f_stopping ==
{stopping:
    - one
    - two
}

== function f_default ==
{one|two}

== function f_cycle ==
{cycle:
    - one
    - two
}

== function f_shuffle ==
{shuffle:
    - one
    - two
}

== function f_shuffle_stopping ==
{stopping shuffle:
    - one
    - two
    - final
}

== function f_shuffle_once ==
{shuffle once:
    - one
    - two
}
                ";

            Story story = CompileString(storyStr);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Once: one two\nStopping: one two two two\nDefault: one two two two\nCycle: one two one two\nShuffle: two one two one\nShuffle stopping: one two final final\nShuffle once: two one\n"));
        }


        [Test()]
        public void TestCallStackEvaluation()
        {
            var storyStr =
                @"
                   { six() + two() }
                    -> END

                === function six
                    ~ return four() + two()

                === function four
                    ~ return two() + two()

                === function two
                    ~ return 2
                ";

            Story story = CompileString(storyStr);
            Assert.That(story.Continue(), Is.EqualTo("8\n"));
        }

        [Test()]
        public void TestChoiceCount()
        {
            Story story = CompileString(@"
<- choices
{ CHOICE_COUNT() }

= end
-> END

= choices
* one -> end
* two -> end
");

            Assert.That(story.Continue(), Is.EqualTo("2\n"));
        }

        [Test()]
        public void TestChoiceDivertsToDone()
        {
            var story = CompileString(@"* choice -> DONE");

            story.Continue();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            story.ChooseChoiceIndex(0);

            Assert.That(story.Continue(), Is.EqualTo("choice"));
        }

        [Test()]
        public void TestChoiceWithBracketsOnly()
        {
            var storyStr = "*   [Option]\n    Text";

            Story story = CompileString(storyStr);
            story.Continue();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("Option"));

            story.ChooseChoiceIndex(0);

            Assert.That(story.Continue(), Is.EqualTo("Text\n"));
        }

        [Test()]
        public void TestCommentEliminator()
        {
            var testContent =
@"A// C
A /* C */ A

A * A * /* * C *// A/*
C C C

*/";

            CommentEliminator p = new CommentEliminator(testContent);
            var result = p.Process();

            var expected = "A\nA  A\n\nA * A * / A\n\n\n";

            Assert.That(result.Replace("\r", ""), Is.EqualTo(expected.Replace("\r", ""))); //Windows perculiarity
        }

        //------------------------------------------------------------------------
        [Test()]
        public void TestCommentEliminatorMixedNewlines()
        {
            var testContent =
                "A B\nC D // comment\nA B\r\nC D // comment\r\n/* block comment\r\nsecond line\r\n */ ";

            CommentEliminator p = new CommentEliminator(testContent);
            var result = p.Process();

            var expected =
                "A B\nC D \nA B\nC D \n\n\n ";

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test()]
        public void TestCompareDivertTargets()
        {
            var storyStr = @"
VAR to_one = -> one
VAR to_two = -> two

{to_one == to_two:same knot|different knot}
{to_one == to_one:same knot|different knot}
{to_two == to_two:same knot|different knot}
{ -> one == -> two:same knot|different knot}
{ -> one == to_one:same knot|different knot}
{ to_one == -> one:same knot|different knot}

== one
    One
    -> DONE

=== two
    Two
    -> DONE";

            Story story = CompileString(storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("different knot\nsame knot\nsame knot\ndifferent knot\nsame knot\nsame knot\n"));
        }

        [Test()]
        public void TestComplexTunnels()
        {
            Story story = CompileString(@"
-> one (1) -> two (2) ->
three (3)

== one(num) ==
one ({num})
-> oneAndAHalf (1.5) ->
->->

== oneAndAHalf(num) ==
one and a half ({num})
->->

== two (num) ==
two ({num})
->->
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("one (1)\none and a half (1" + System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator + "5)\ntwo (2)\nthree (3)\n"));
        }

        [Test()]
        public void TestConditionalChoiceInWeave()
        {
            var storyStr =
                @"
- start
 {
    - true: * [go to a stitch] -> a_stitch
 }
- gather should be seen
-> DONE

= a_stitch
    result
    -> END
                ";

            Story story = CompileString(storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("start\ngather should be seen\n"));
            Assert.That(story.currentChoices.Count, Is.EqualTo(1));

            story.ChooseChoiceIndex(0);

            Assert.That(story.Continue(), Is.EqualTo("result\n"));
        }

        [Test()]
        public void TestConditionalChoiceInWeave2()
        {
            var storyStr =
                @"
- first gather
    * [option 1]
    * [option 2]
- the main gather
{false:
    * unreachable option -> END
}
- bottom gather";

            Story story = CompileString(storyStr);

            Assert.That(story.Continue(), Is.EqualTo("first gather\n"));

            Assert.That(story.currentChoices.Count, Is.EqualTo(2));

            story.ChooseChoiceIndex(0);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("the main gather\nbottom gather\n"));
            Assert.That(story.currentChoices, Is.Empty);
        }

        [Test()]
        public void TestConditionalChoices()
        {
            var storyStr =
                @"
* { true } { false } not displayed
* { true } { true }
  { true and true }  one
* { false } not displayed
* (name) { true } two
* { true }
  { true }
  three
* { true }
  four
                ";

            Story story = CompileString(storyStr);
            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(4));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("one"));
            Assert.That(story.currentChoices[1].text, Is.EqualTo("two"));
            Assert.That(story.currentChoices[2].text, Is.EqualTo("three"));
            Assert.That(story.currentChoices[3].text, Is.EqualTo("four"));
        }

        [Test()]
        public void TestConditionals()
        {
            var storyStr =
                @"
{false:not true|true}
{
   - 4 > 5: not true
   - 5 > 4: true
}
{ 2*2 > 3:
   - true
   - not true
}
{
   - 1 > 3: not true
   - { 2+2 == 4:
        - true
        - not true
   }
}
{ 2*3:
   - 1+7: not true
   - 9: not true
   - 1+1+1+3: true
   - 9-3: also true but not printed
}
{ true:
    great
    right?
}
                ";

            Story story = CompileString(storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("true\ntrue\ntrue\ntrue\ntrue\ngreat\nright?\n"));
        }

        [Test()]
        public void TestConst()
        {
            var story = CompileString(@"
VAR x = c

CONST c = 5

{x}
");
            Assert.That(story.Continue(), Is.EqualTo("5\n"));
        }

        [Test()]
        public void TestDefaultChoices()
        {
            Story story = CompileString(@"
 - (start)
 * [Choice 1]
 * [Choice 2]
 * {false} Impossible choice
 * -> default
 - After choice
 -> start

== default ==
This is default.
-> DONE
");

            Assert.That(story.Continue(), Is.Empty);
            Assert.That(story.currentChoices.Count, Is.EqualTo(2));

            story.ChooseChoiceIndex(0);
            Assert.That(story.Continue(), Is.EqualTo("After choice\n"));

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("After choice\nThis is default.\n"));
        }

        [Test()]
        public void TestDefaultSimpleGather()
        {
            var story = CompileString(@"
* ->
- x
-> DONE");

            Assert.That(story.Continue(), Is.EqualTo("x\n"));
        }

        [Test()]
        public void TestDivertInConditional()
        {
            var storyStr =
                @"
=== intro
= top
    { main: -> done }
    -> END
= main
    -> top
= done
    -> END
                ";

            Story story = CompileString(storyStr);
            Assert.That(story.ContinueMaximally(), Is.Empty);
        }

        [Test()]
        public void TestDivertNotFoundError()
        {
            CompileStringWithoutRuntime(@"
-> knot

== knot ==
Knot.
-> next
", testingErrors: true);

            Assert.That(HadError("not found"), Is.True);
        }

        [Test()]
        public void TestDivertToWeavePoints()
        {
            var storyStr =
                @"
-> knot.stitch.gather

== knot ==
= stitch
- hello
    * (choice) test
        choice content
- (gather)
  gather

  {stopping:
    - -> knot.stitch.choice
    - second time round
  }

-> END
                ";

            Story story = CompileString(storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("gather\ntest\nchoice content\ngather\nsecond time round\n"));
        }

        public void TestElseBranches()
        {
            var storyStr =
                @"
VAR x = 3

{
    - x == 1: one
    - x == 2: two
    - else: other
}

{
    - x == 1: one
    - x == 2: two
    - other
}

{ x == 4:
  - The main clause
  - else: other
}

{ x == 4:
  The main clause
- else:
  other
}
                ";

            Story story = CompileString(storyStr);

            Assert.That(story.currentText, Is.EqualTo("other\nother\nother\nother\n"));
        }

        [Test()]
        public void TestEmpty()
        {
            Story story = CompileString(@"");

            Assert.That(story.currentText, Is.Empty);
        }

        [Test()]
        public void TestEmptyChoice()
        {
            int warningCount = 0;
            InkParser parser = new InkParser("*", null, (string message, ErrorType errorType) =>
            {
                if (errorType == ErrorType.Warning)
                {
                    warningCount++;
                    Assert.That(message.Contains("completely empty"), Is.True);
                }
                else
                {
                    Assert.Fail("Shouldn't have had any errors");
                }
            });

            parser.Parse();

            Assert.That(warningCount, Is.EqualTo(1));
        }

        [Test()]
        public void TestEmptyMultilineConditionalBranch()
        {
            var story = CompileString(@"
{ 3:
    - 3:
    - 4:
        txt
}
");

            Assert.That(story.Continue(), Is.Empty);
        }


        [Test ()]
        public void TestAllSwitchBranchesFailIsClean ()
        {
        	var story = CompileString (@"
{ 1:
    - 2: x
    - 3: y
}
        ");

            story.Continue ();

            Assert.That(story.state.evaluationStack.Count == 0, Is.True);
        }

        [Test ()]
        public void TestTrivialCondition ()
        {
        	var story = CompileString (@"
{
- false:
   beep
}
                ");

        	story.Continue ();
        }

        [Test()]
        public void TestEmptySequenceContent()
        {
            var story = CompileString(@"
-> thing ->
-> thing ->
-> thing ->
-> thing ->
-> thing ->
Done.

== thing ==
{once:
  - Wait for it....
  -
  -
  -  Surprise!
}
->->
");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Wait for it....\nSurprise!\nDone.\n"));
        }

        [Test()]
        public void TestEnd()
        {
            Story story = CompileString(@"
hello
-> END
world
-> END
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("hello\n"));
        }

        [Test()]
        public void TestEnd2()
        {
            Story story = CompileString(@"
-> test

== test ==
hello
-> END
world
-> END
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("hello\n"));
        }

        [Test()]
        public void TestEndOfContent()
        {
            Story story = CompileString("Hello world", false, true);
            story.ContinueMaximally();
            Assert.That(HadError(), Is.False);

            story = CompileString("== test ==\nContent\n-> END");
            story.ContinueMaximally();

            // Should have runtime error due to running out of content
            // (needs a -> END)
            story = CompileString("== test ==\nContent", false, true);
            story.ContinueMaximally();
            Assert.That(HadWarning(), Is.True);

            // Should have warning that there's no "-> END"
            CompileStringWithoutRuntime("== test ==\nContent", true);
            Assert.That(HadError(), Is.False);
            Assert.That(HadWarning(), Is.True);

            CompileStringWithoutRuntime("== test ==\n~return", testingErrors: true);
            Assert.That(HadError("Return statements can only be used in knots that are declared as functions"), Is.True);

            CompileStringWithoutRuntime("== function test ==\n-> END", testingErrors: true);
            Assert.That(HadError("Functions may not contain diverts"), Is.True);
        }

        [Test()]
        public void TestEscapeCharacter()
        {
            var storyStr = @"{true:this is a '\|' character|this isn't}";

            Story story = CompileString(storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("this is a '|' character\n"));
        }

        [Test()]
        public void TestExternalBinding()
        {
            var story = CompileString(@"
EXTERNAL message(x)
EXTERNAL multiply(x,y)
EXTERNAL times(i,str)
~ message(""hello world"")
{multiply(5.0, 3)}
{times(3, ""knock "")}
");
            string message = null;

            story.BindExternalFunction("message", (string arg) =>
            {
                message = "MESSAGE: " + arg;
            });

            story.BindExternalFunction("multiply", (float arg1, int arg2) =>
            {
                return arg1 * arg2;
            });

            story.BindExternalFunction("times", (int numberOfTimes, string str) =>
            {
                string result = "";
                for (int i = 0; i < numberOfTimes; i++)
                {
                    result += str;
                }
                return result;
            });

            Assert.That(story.Continue(), Is.EqualTo("15\n"));

            Assert.That(story.Continue(), Is.EqualTo("knock knock knock\n"));

            Assert.That(message, Is.EqualTo("MESSAGE: hello world"));
        }

        [Test()]
        public void TestLookupSafeOrNot()
        {
            var story = CompileString(@"
EXTERNAL myAction()

One
~ myAction()
Two
");

            // Lookahead SAFE - should get multiple calls to the ext function,
            // one for lookahead on first line, one "for real" on second line.
            int callCount = 0;
            story.BindExternalFunction("myAction", () => callCount++, lookaheadSafe:true);

            story.ContinueMaximally();
            Assert.That(callCount, Is.EqualTo(2));

            // Lookahead UNSAFE - when it sees the function, it should break out early
            // and stop lookahead, making sure that the action is only called for the second line.
            callCount = 0;
            story.ResetState();
            story.UnbindExternalFunction("myAction");
            story.BindExternalFunction("myAction", () => callCount++, lookaheadSafe:false);

            story.ContinueMaximally();
            Assert.That(callCount, Is.EqualTo(1));

            // Lookahead SAFE but breaks glue intentionally
            var storyWithPostGlue = CompileString(@"
EXTERNAL myAction()

One 
~ myAction()
<> Two
");

            storyWithPostGlue.BindExternalFunction("myAction", () => {});
            var result = storyWithPostGlue.ContinueMaximally();
            Assert.That(result, Is.EqualTo("One\nTwo\n"));
        }

        [Test()]
        public void TestFactorialByReference()
        {
            var storyStr = @"
VAR result = 0
~ factorialByRef(result, 5)
{ result }

== function factorialByRef(ref r, n) ==
{ r == 0:
    ~ r = 1
}
{ n > 1:
    ~ r = r * n
    ~ factorialByRef(r, n-1)
}
~ return
";

            Story story = CompileString(storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("120\n"));
        }

        [Test()]
        public void TestFactorialRecursive()
        {
            var storyStr = @"
{ factorial(5) }

== function factorial(n) ==
 { n == 1:
    ~ return 1
 - else:
    ~ return (n * factorial(n-1))
 }
";

            Story story = CompileString(storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("120\n"));
        }

        [Test()]
        public void TestFunctionCallRestrictions()
        {
            CompileStringWithoutRuntime(@"
// Allowed to do this
~ myFunc()

// Not allowed to to this
~ aKnot()

// Not allowed to do this
-> myFunc

== function myFunc ==
This is a function.
~ return

== aKnot ==
This is a normal knot.
-> END
", testingErrors: true);

            Assert.That(_errorMessages.Count, Is.EqualTo(2));
            Assert.That(_errorMessages[0].Contains("hasn't been marked as a function"), Is.True);
            Assert.That(_errorMessages[1].Contains("can only be called as a function"), Is.True);
        }

        [Test()]
        public void TestFunctionPurityChecks()
        {
            CompileStringWithoutRuntime(@"
-> test

== test ==
~ myFunc()
= function myBadInnerFunc
Not allowed!
~ return

== function myFunc ==
Hello world
* a choice
* another choice
-
-> myFunc
= testStitch
    This is a stitch
~ return
", testingErrors: true);

            Assert.That(_errorMessages.Count, Is.EqualTo(7));
            Assert.That(_errorMessages[0].Contains("Return statements can only be used in knots that"), Is.True);
            Assert.That(_errorMessages[1].Contains("Functions cannot be stitches"), Is.True);
            Assert.That(_errorMessages[2].Contains("Functions may not contain stitches"), Is.True);
            Assert.That(_errorMessages[3].Contains("Functions may not contain diverts"), Is.True);
            Assert.That(_errorMessages[4].Contains("Functions may not contain choices"), Is.True);
            Assert.That(_errorMessages[5].Contains("Functions may not contain choices"), Is.True);
            Assert.That(_errorMessages[6].Contains("Return statements can only be used in knots that"), Is.True);
        }

        [Test()]
        public void TestDisallowEmptyDiverts()
        {
            CompileStringWithoutRuntime ("->", testingErrors: true);

            Assert.That(HadError("Empty diverts (->) are only valid on choices"), Is.True);
        }

        [Test()]
        public void TestGatherChoiceSameLine()
        {
            var storyStr = "- * hello\n- * world";

            Story story = CompileString(storyStr);
            story.Continue();

            Assert.That(story.currentChoices[0].text, Is.EqualTo("hello"));

            story.ChooseChoiceIndex(0);
            story.Continue();

            Assert.That(story.currentChoices[0].text, Is.EqualTo("world"));
        }

        [Test()]
        public void TestGatherReadCountWithInitialSequence()
        {
            var story = CompileString(@"
- (opts)
{test:seen test}
- (test)
{ -> opts |}
");

            Assert.That(story.Continue(), Is.EqualTo("seen test\n"));
        }

        [Test()]
        public void TestHasReadOnChoice()
        {
            var storyStr =
                @"
* { not test } visible choice
* { test } visible choice

== test ==
-> END
                ";

            Story story = CompileString(storyStr);
            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("visible choice"));
        }

        [Test()]
        public void TestHelloWorld()
        {
            Story story = CompileString("Hello world");
            Assert.That(story.Continue(), Is.EqualTo("Hello world\n"));
        }

        [Test()]
        public void TestIdentifersCanStartWithNumbers()
        {
            var story = CompileString(@"
-> 2tests
== 2tests ==
~ temp 512x2 = 512 * 2
~ temp 512x2p2 = 512x2 + 2
512x2 = {512x2}
512x2p2 = {512x2p2}
-> DONE
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("512x2 = 1024\n512x2p2 = 1026\n"));
        }

        [Test()]
        public void TestImplicitInlineGlue()
        {
            var story = CompileString(@"
I have {five()} eggs.

== function five ==
{false:
    Don't print this
}
five
");

            Assert.That(story.Continue(), Is.EqualTo("I have five eggs.\n"));
        }

        [Test ()]
        public void TestImplicitInlineGlueB ()
        {
            var story = CompileString (@"
A {f():B} 
X

=== function f() ===
{true: 
    ~ return false
}
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("A\nX\n"));
        }

        [Test ()]
        public void TestImplicitInlineGlueC ()
        {
            var story = CompileString (@"
A
{f():X}
C

=== function f()
{ true: 
    ~ return false
}
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("A\nC\n"));
        }

        [Test()]
        public void TestInclude()
        {
            var storyStr =
                @"
INCLUDE test_included_file.ink
  INCLUDE test_included_file2.ink

This is the main file.
                ";

            Story story = CompileString(storyStr);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("This is include 1.\nThis is include 2.\nThis is the main file.\n"));
        }

        [Test()]
        public void TestIncrement()
        {
            Story story = CompileString(@"
VAR x = 5
~ x++
{x}

~ x--
{x}
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("6\n5\n"));
        }

        [Test()]
        public void TestKnotDotGather()
        {
            var story = CompileString(@"
-> knot
=== knot
-> knot.gather
- (gather) g
-> DONE");

            Assert.That(story.Continue(), Is.EqualTo("g\n"));
        }

        // Although VAR and CONST declarations are parsed as being
        // part of the knot, they're extracted, so that the null
        // termination detection shouldn't see this as a loose end.
        [Test()]
        public void TestKnotTerminationSkipsGlobalObjects()
        {
            CompileStringWithoutRuntime(@"
=== stuff ===
-> END

VAR X = 1
CONST Y = 2
", testingErrors: true);

            Assert.That(_warningMessages.Count == 0, Is.True);
        }


        [Test ()]
        public void TestLooseEnds ()
        {
        	CompileStringWithoutRuntime (
@"No loose ends in main content.

== knot1 ==
* loose end choice
* loose end
	on second line of choice

== knot2 ==
* A
* B
TODO: Fix loose ends but don't warn

== knot3 ==
Loose end when there's no weave

== knot4 ==
{true:
    {false:
        Ignore loose end when there's a divert
        in a conditional.
        -> knot4
	}
}
        ", testingErrors: true);

            Assert.That(_warningMessages.Count == 3, Is.True);
            Assert.That(HadWarning("line 4: Apparent loose end"), Is.True);
            Assert.That(HadWarning("line 6: Apparent loose end"), Is.True);
            Assert.That(HadWarning("line 14: Apparent loose end"), Is.True);
            Assert.That(_authorMessages.Count == 1, Is.True);
        }

        [Test()]
        public void TestKnotThreadInteraction()
        {
            Story story = CompileString(@"
-> knot
=== knot
    <- threadB
    -> tunnel ->
    THE END
    -> END

=== tunnel
    - blah blah
    * wigwag
    - ->->

=== threadB
    *   option
    -   something
        -> DONE
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("blah blah\n"));

            Assert.That(story.currentChoices.Count, Is.EqualTo(2));
            Assert.That(story.currentChoices[0].text.Contains("option"), Is.True);
            Assert.That(story.currentChoices[1].text.Contains("wigwag"), Is.True);

            story.ChooseChoiceIndex(1);
            Assert.That(story.Continue(), Is.EqualTo("wigwag\n"));
            Assert.That(story.Continue(), Is.EqualTo("THE END\n"));
        }

        [Test()]
        public void TestKnotThreadInteraction2()
        {
            Story story = CompileString(@"
-> knot
=== knot
    <- threadA
    When should this get printed?
    -> DONE

=== threadA
    -> tunnel ->
    Finishing thread.
    -> DONE

=== tunnel
    -   I’m in a tunnel
    *   I’m an option
    -   ->->

");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("I’m in a tunnel\nWhen should this get printed?\n"));
            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("I’m an option"));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("I’m an option\nFinishing thread.\n"));
        }

        [Test()]
        public void TestLeadingNewlineMultilineSequence()
        {
            var story = CompileString(@"
{stopping:

- a line after an empty line
- blah
}
");

            Assert.That(story.Continue(), Is.EqualTo("a line after an empty line\n"));
        }

        [Test()]
        public void TestLiteralUnary()
        {
            var story = CompileString(@"
VAR negativeLiteral = -1
VAR negativeLiteral2 = not not false
VAR negativeLiteral3 = !(0)

{negativeLiteral}
{negativeLiteral2}
{negativeLiteral3}
");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("-1\nfalse\ntrue\n"));
        }

        [Test()]
        public void TestLogicInChoices()
        {
            var story = CompileString(@"
* 'Hello {name()}[, your name is {name()}.'],' I said, knowing full well that his name was {name()}.
-> DONE

== function name ==
Joe
");

            story.ContinueMaximally();

            Assert.That(story.currentChoices[0].text, Is.EqualTo("'Hello Joe, your name is Joe.'"));
            story.ChooseChoiceIndex(0);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("'Hello Joe,' I said, knowing full well that his name was Joe.\n"));
        }

        [Test()]
        public void TestMultipleConstantReferences()
        {
            var story = CompileString(@"
CONST CONST_STR = ""ConstantString""
VAR varStr = CONST_STR
{varStr == CONST_STR:success}
");

            Assert.That(story.Continue(), Is.EqualTo("success\n"));
        }

        [Test()]
        public void TestMultiThread()
        {
            Story story = CompileString(@"
-> start
== start ==
-> tunnel ->
The end
-> END

== tunnel ==
<- place1
<- place2
-> DONE

== place1 ==
This is place 1.
* choice in place 1
- ->->

== place2 ==
This is place 2.
* choice in place 2
- ->->
");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("This is place 1.\nThis is place 2.\n"));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("choice in place 1\nThe end\n"));
        }

        [Test()]
        public void TestNestedInclude()
        {
            var storyStr =
                @"
INCLUDE test_included_file3.ink

This is the main file

-> knot_in_2
                ";

            Story story = CompileString(storyStr);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("The value of a variable in test file 2 is 5.\nThis is the main file\nThe value when accessed from knot_in_2 is 5.\n"));
        }

        [Test()]
        public void TestNestedPassByReference()
        {
            var storyStr = @"
VAR globalVal = 5

{globalVal}

~ squaresquare(globalVal)

{globalVal}

== function squaresquare(ref x) ==
 {square(x)} {square(x)}
 ~ return

== function square(ref x) ==
 ~ x = x * x
 ~ return
";

            Story story = CompileString(storyStr);

            // Bloody whitespace
            Assert.That(story.ContinueMaximally(), Is.EqualTo("5\n625\n"));
        }

        [Test()]
        public void TestNonTextInChoiceInnerContent()
        {
            var storyStr =
                @"
-> knot
== knot
   *   option text[]. {true: Conditional bit.} -> next
   -> DONE

== next
    Next.
    -> DONE
                ";

            Story story = CompileString(storyStr);
            story.Continue();

            story.ChooseChoiceIndex(0);
            Assert.That(story.Continue(), Is.EqualTo("option text. Conditional bit. Next.\n"));
        }

        [Test()]
        public void TestOnceOnlyChoicesCanLinkBackToSelf()
        {
            var story = CompileString(@"
-> opts
= opts
*   (firstOpt) [First choice]   ->  opts
*   {firstOpt} [Second choice]  ->  opts
* -> end

- (end)
    -> END
");

            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("First choice"));

            story.ChooseChoiceIndex(0);
            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("Second choice"));

            story.ChooseChoiceIndex(0);
            story.ContinueMaximally();

            Assert.That(story.currentErrors, Is.Null);
        }

        [Test()]
        public void TestOnceOnlyChoicesWithOwnContent()
        {
            Story story = CompileString(@"
VAR times = 3
-> home

== home ==
~ times = times - 1
{times >= 0:-> eat}
I've finished eating now.
-> END

== eat ==
This is the {first|second|third} time.
 * Eat ice-cream[]
 * Drink coke[]
 * Munch cookies[]
-
-> home
");

            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(3));

            story.ChooseChoiceIndex(0);
            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(2));

            story.ChooseChoiceIndex(0);
            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));

            story.ChooseChoiceIndex(0);
            story.ContinueMaximally();

            Assert.That(story.currentChoices, Is.Empty);
        }

        [Test()]
        public void TestPaths()
        {
            // Different instances should ensure different instances of individual components
            var path1 = new Path("hello.1.world");
            var path2 = new Path("hello.1.world");

            var path3 = new Path(".hello.1.world");
            var path4 = new Path(".hello.1.world");

            Assert.That(path2, Is.EqualTo(path1));

            Assert.That(path4, Is.EqualTo(path3));

            Assert.That(path3, Is.Not.EqualTo(path1));
        }

        [Test()]
        public void TestPathToSelf()
        {
            var story = CompileString(@"
- (dododo)
-> tunnel ->
-> dododo

== tunnel
+ A
->->
");
            // We're only checking that the story copes
            // okay without crashing
            // (internally the "-> dododo" ends up generating
            //  a very short path: ".^", and after walking into
            // the parent, it didn't cope with the "." before
            // I fixed it!)
            story.Continue();
            story.ChooseChoiceIndex(0);
            story.Continue();
            story.ChooseChoiceIndex(0);
        }

        [Test()]
        public void TestPrintNum()
        {
            var story = CompileString(@"
. {print_num(4)} .
. {print_num(15)} .
. {print_num(37)} .
. {print_num(101)} .
. {print_num(222)} .
. {print_num(1234)} .

=== function print_num(x) ===
{
    - x >= 1000:
        {print_num(x / 1000)} thousand { x mod 1000 > 0:{print_num(x mod 1000)}}
    - x >= 100:
        {print_num(x / 100)} hundred { x mod 100 > 0:and {print_num(x mod 100)}}
    - x == 0:
        zero
    - else:
        { x >= 20:
            { x / 10:
                - 2: twenty
                - 3: thirty
                - 4: forty
                - 5: fifty
                - 6: sixty
                - 7: seventy
                - 8: eighty
                - 9: ninety
            }
            { x mod 10 > 0:<>-<>}
        }
        { x < 10 || x > 20:
            { x mod 10:
                - 1: one
                - 2: two
                - 3: three
                - 4: four
                - 5: five
                - 6: six
                - 7: seven
                - 8: eight
                - 9: nine
            }
        - else:
            { x:
                - 10: ten
                - 11: eleven
                - 12: twelve
                - 13: thirteen
                - 14: fourteen
                - 15: fifteen
                - 16: sixteen
                - 17: seventeen
                - 18: eighteen
                - 19: nineteen
            }
        }
}
");

            Assert.That(
story.ContinueMaximally().Replace("\r", ""), Is.EqualTo(@". four .
. fifteen .
. thirty-seven .
. one hundred and one .
. two hundred and twenty-two .
. one thousand two hundred and thirty-four .
".Replace("\r", "")));
        }

        [Test()]
        public void TestQuoteCharacterSignificance()
        {
            // Confusing escaping + ink! Actual ink string is:
            // My name is "{"J{"o"}e"}"
            //  - First and last quotes are insignificant - they're part of the content
            //  - Inner quotes are significant - they're part of the syntax for string expressions
            // So output is: My name is "Joe"
            var story = CompileString(@"My name is ""{""J{""o""}e""}""");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("My name is \"Joe\"\n"));
        }

        [Test()]
        public void TestReadCountAcrossCallstack()
        {
            var story = CompileString(@"
-> first

== first ==
1) Seen first {first} times.
-> second ->
2) Seen first {first} times.
-> DONE

== second ==
In second.
->->
");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("1) Seen first 1 times.\nIn second.\n2) Seen first 1 times.\n"));
        }

        [Test()]
        public void TestReadCountAcrossThreads()
        {
            var story = CompileString(@"
    -> top

= top
    {top}
    <- aside
    {top}
    -> DONE

= aside
    * {false} DONE
	- -> DONE
");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("1\n1\n"));
        }

        [Test()]
        public void TestReadCountDotSeparatedPath()
        {
            Story story = CompileString(@"
-> hi ->
-> hi ->
-> hi ->

{ hi.stitch_to_count }

== hi ==
= stitch_to_count
hi
->->
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("hi\nhi\nhi\n3\n"));
        }

        [Test()]
        public void TestRequireVariableTargetsTyped()
        {
            CompileStringWithoutRuntime(@"
-> test(-> elsewhere)

== test(varTarget) ==
-> varTarget ->
-> DONE

== elsewhere ==
->->
", testingErrors: true);
            Assert.That(HadError("it should be marked as: ->"), Is.True);
        }

        [Test()]
        public void TestReturnTextWarning()
        {
            InkParser parser = new InkParser("== test ==\n return something",
                null,
                (string message, ErrorType errorType) =>
                {
                    if (errorType == ErrorType.Warning)
                    {
                        throw new TestWarningException();
                    }
                });

            Assert.Throws<TestWarningException>(() => parser.Parse());
        }

        [Test()]
        public void TestSameLineDivertIsInline()
        {
            var story = CompileString(@"
-> hurry_home
=== hurry_home ===
We hurried home to Savile Row -> as_fast_as_we_could

=== as_fast_as_we_could ===
as fast as we could.
-> DONE
");

            Assert.That(story.Continue(), Is.EqualTo("We hurried home to Savile Row as fast as we could.\n"));
        }

        [Test()]
        public void TestShouldntGatherDueToChoice()
        {
            Story story = CompileString(@"
* opt
    - - text
    * * {false} impossible
    * * -> END
- gather");

            story.ContinueMaximally();
            story.ChooseChoiceIndex(0);

            // Shouldn't go to "gather"
            Assert.That(story.ContinueMaximally(), Is.EqualTo("opt\ntext\n"));
        }

        [Test()]
        public void TestShuffleStackMuddying()
        {
            var story = CompileString (@"
* {condFunc()} [choice 1]
* {condFunc()} [choice 2]
* {condFunc()} [choice 3]
* {condFunc()} [choice 4]


=== function condFunc() ===
{shuffle:
    - ~ return false
    - ~ return true
    - ~ return true
    - ~ return false
}
");

            story.Continue ();

            Assert.That(story.currentChoices.Count, Is.EqualTo(2));
        }

        [Test()]
        public void TestSimpleGlue()
        {
            var storyStr = "Some <> \ncontent<> with glue.\n";

            Story story = CompileString(storyStr);

            Assert.That(story.Continue(), Is.EqualTo("Some content with glue.\n"));
        }

        [Test()]
        public void TestStickyChoicesStaySticky()
        {
            var story = CompileString(@"
-> test
== test ==
First line.
Second line.
+ Choice 1
+ Choice 2
- -> test
");

            story.ContinueMaximally();
            Assert.That(story.currentChoices.Count, Is.EqualTo(2));

            story.ChooseChoiceIndex(0);
            story.ContinueMaximally();
            Assert.That(story.currentChoices.Count, Is.EqualTo(2));
        }

        [Test()]
        public void TestStringConstants()
        {
            var story = CompileString(@"
{x}
VAR x = kX
CONST kX = ""hi""
");

            Assert.That(story.Continue(), Is.EqualTo("hi\n"));
        }

        [Test()]
        public void TestStringParserA()
        {
            StringParser p = new StringParser("A");
            var results = p.Interleave<string>(
                () => p.ParseString("A"),
                () => p.ParseString("B"));

            var expected = new[] { "A" };
            Assert.That(results, Is.EqualTo(expected));
        }

        [Test()]
        public void TestStringParserABAB()
        {
            StringParser p = new StringParser("ABAB");
            var results = p.Interleave<string>(
                () => p.ParseString("A"),
                () => p.ParseString("B"));

            var expected = new[] { "A", "B", "A", "B" };
            Assert.That(results, Is.EqualTo(expected));
        }

        [Test()]
        public void TestStringParserABAOptional()
        {
            StringParser p = new StringParser("ABAA");
            var results = p.Interleave<string>(
                () => p.ParseString("A"),
                p.Optional(() => p.ParseString("B")));

            var expected = new[] { "A", "B", "A", "A" };
            Assert.That(results, Is.EqualTo(expected));
        }

        [Test()]
        public void TestStringParserABAOptional2()
        {
            StringParser p = new StringParser("BABB");
            var results = p.Interleave<string>(
                p.Optional(() => p.ParseString("A")),
                () => p.ParseString("B"));

            var expected = new[] { "B", "A", "B", "B" };
            Assert.That(results, Is.EqualTo(expected));
        }

        [Test()]
        public void TestStringParserB()
        {
            StringParser p = new StringParser("B");
            var result = p.Interleave<string>(
                () => p.ParseString("A"),
                () => p.ParseString("B"));

            Assert.That(result, Is.Null);
        }

        [Test()]
        public void TestStringsInChoices()
        {
            var story = CompileString(@"
* \ {""test1""} [""test2 {""test3""}""] {""test4""}
-> DONE
");
            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo(@"test1 ""test2 test3"""));

            story.ChooseChoiceIndex(0);
            Assert.That(story.Continue(), Is.EqualTo("test1 test4\n"));
        }

        [Test()]
        public void TestStringTypeCoersion()
        {
            var story = CompileString(@"
{""5"" == 5:same|different}
{""blah"" == 5:same|different}
");

            // Not sure that "5" should be equal to 5, but hmm.
            Assert.That(story.ContinueMaximally(), Is.EqualTo("same\ndifferent\n"));
        }

        [Test()]
        public void TestTemporariesAtGlobalScope()
        {
            var story = CompileString(@"
VAR x = 5
~ temp y = 4
{x}{y}
");
            Assert.That(story.Continue(), Is.EqualTo("54\n"));
        }

        [Test()]
        public void TestThreadDone()
        {
            Story story = CompileString(@"
This is a thread example
<- example_thread
The example is now complete.

== example_thread ==
Hello.
-> DONE
World.
-> DONE
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("This is a thread example\nHello.\nThe example is now complete.\n"));
        }

        [Test()]
        public void TestTunnelOnwardsAfterTunnel()
        {
            var story = CompileString(@"
-> tunnel1 ->
The End.
-> END

== tunnel1 ==
Hello...
-> tunnel2 ->->

== tunnel2 ==
...world.
->->
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("Hello...\n...world.\nThe End.\n"));
        }

        [Test()]
        public void TestTunnelVsThreadBehaviour()
        {
            Story story = CompileString(@"
-> knot_with_options ->
Finished tunnel.

Starting thread.
<- thread_with_options
* E
-
Done.

== knot_with_options ==
* A
* B
-
->->

== thread_with_options ==
* C
* D
- -> DONE
");

            Assert.That(story.ContinueMaximally().Contains("Finished tunnel"), Is.False);

            // Choices should be A, B
            Assert.That(story.currentChoices.Count, Is.EqualTo(2));

            story.ChooseChoiceIndex(0);

            // Choices should be C, D, E
            Assert.That(story.ContinueMaximally().Contains("Finished tunnel"), Is.True);
            Assert.That(story.currentChoices.Count, Is.EqualTo(3));

            story.ChooseChoiceIndex(2);

            Assert.That(story.ContinueMaximally().Contains("Done."), Is.True);
        }

        [Test()]
        public void TestTurnsSince()
        {
            Story story = CompileString(@"
{ TURNS_SINCE(-> test) }
~ test()
{ TURNS_SINCE(-> test) }
* [choice 1]
- { TURNS_SINCE(-> test) }
* [choice 2]
- { TURNS_SINCE(-> test) }

== function test ==
~ return
");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("-1\n0\n"));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("1\n"));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("2\n"));
        }

        [Test()]
        public void TestTurnsSinceNested()
        {
            var story = CompileString(@"
-> empty_world
=== empty_world ===
    {TURNS_SINCE(-> then)} = -1
    * (then) stuff
        {TURNS_SINCE(-> then)} = 0
        * * (next) more stuff
            {TURNS_SINCE(-> then)} = 1
        -> DONE
");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("-1 = -1\n"));

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            story.ChooseChoiceIndex(0);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("stuff\n0 = 0\n"));

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            story.ChooseChoiceIndex(0);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("more stuff\n1 = 1\n"));
        }

        [Test()]
        public void TestTurnsSinceWithVariableTarget()
        {
            // Count all visits must be switched on for variable count targets
            var story = CompileString(@"
-> start

=== start ===
    {beats(-> start)}
    {beats(-> start)}
    *   [Choice]  -> next
= next
    {beats(-> start)}
    -> END

=== function beats(x) ===
    ~ return TURNS_SINCE(x)
", countAllVisits: true);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("0\n0\n"));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("1\n"));
        }

        [Test()]
        public void TestUnbalancedWeaveIndentation()
        {
            var story = CompileString(@"
* * * First
* * * * Very indented
- - End
-> END
");
            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("First"));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("First\n"));
            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("Very indented"));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Very indented\nEnd\n"));
            Assert.That(story.currentChoices, Is.Empty);
        }

        [Test()]
        public void TestVariableDeclarationInConditional()
        {
            var storyStr =
                @"
VAR x = 0
{true:
    - ~ x = 5
}
{x}
                ";

            Story story = CompileString(storyStr);

            // Extra newline is because there's a choice object sandwiched there,
            // so it can't be absorbed :-/
            Assert.That(story.Continue(), Is.EqualTo("5\n"));
        }

        [Test()]
        public void TestVariableDivertTarget()
        {
            var story = CompileString(@"
VAR x = -> here

-> there

== there ==
-> x

== here ==
Here.
-> DONE
");
            Assert.That(story.Continue(), Is.EqualTo("Here.\n"));
        }

        [Test()]
        public void TestVariableGetSetAPI()
        {
            var story = CompileString(@"
VAR x = 5

{x}

* [choice]
-
{x}

* [choice]
-

{x}

* [choice]
-

{x}

-> DONE
");

            // Initial state
            Assert.That(story.ContinueMaximally(), Is.EqualTo("5\n"));
            Assert.That(story.variablesState["x"], Is.EqualTo(5));

            story.variablesState["x"] = 10;
            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("10\n"));
            Assert.That(story.variablesState["x"], Is.EqualTo(10));

            story.variablesState["x"] = 8.5f;
            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("8" + System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator + "5\n"));
            Assert.That(story.variablesState["x"], Is.EqualTo(8.5f));

            story.variablesState["x"] = "a string";
            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("a string\n"));
            Assert.That(story.variablesState["x"], Is.EqualTo("a string"));

            Assert.That(story.variablesState["z"], Is.Null);

            // Not allowed arbitrary types
            Assert.Throws<Exception>(() =>
            {
                story.variablesState["x"] = new System.Text.StringBuilder();
            });
        }

        [Test()]
        public void TestVariableObserver()
        {
            var story = CompileString(@"
VAR testVar = 5
VAR testVar2 = 10

Hello world!

~ testVar = 15
~ testVar2 = 100

Hello world 2!

* choice

    ~ testVar = 25
    ~ testVar2 = 200

    -> END
");

            int currentVarValue = 0;
            int observerCallCount = 0;

            story.ObserveVariable("testVar", (string varName, object newValue) =>
            {
                currentVarValue = (int)newValue;
                observerCallCount++;
            });

            story.ContinueMaximally();

            Assert.That(currentVarValue, Is.EqualTo(15));
            Assert.That(observerCallCount, Is.EqualTo(1));
            Assert.That(story.currentChoices.Count, Is.EqualTo(1));

            story.ChooseChoiceIndex(0);
            story.Continue();

            Assert.That(currentVarValue, Is.EqualTo(25));
            Assert.That(observerCallCount, Is.EqualTo(2));
        }

        [Test()]
        public void TestVariablePointerRefFromKnot()
        {
            var story = CompileString(@"
VAR val = 5

-> knot ->

-> END

== knot ==
~ inc(val)
{val}
->->

== function inc(ref x) ==
    ~ x = x + 1
");

            Assert.That(story.Continue(), Is.EqualTo("6\n"));
        }

        [Test()]
        public void TestVariableSwapRecurse()
        {
            var storyStr = @"
~ f(1, 1)

== function f(x, y) ==
{ x == 1 and y == 1:
  ~ x = 2
  ~ f(y, x)
- else:
  {x} {y}
}
~ return
";

            Story story = CompileString(storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("1 2\n"));
        }

        [Test()]
        public void TestVariableTunnel()
        {
            var story = CompileString(@"
-> one_then_tother(-> tunnel)

=== one_then_tother(-> x) ===
    -> x -> end

=== tunnel ===
    STUFF
    ->->

=== end ===
    -> END
");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("STUFF\n"));
        }

        [Test()]
        public void TestWeaveGathers()
        {
            var storyStr =
                @"
-
 * one
    * * two
   - - three
 *  four
   - - five
- six
                ";

            Story story = CompileString(storyStr);

            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(2));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("one"));
            Assert.That(story.currentChoices[1].text, Is.EqualTo("four"));

            story.ChooseChoiceIndex(0);
            story.ContinueMaximally();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("two"));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("two\nthree\nsix\n"));
        }

        [Test()]
        public void TestWeaveOptions()
        {
            var storyStr =
                @"
                    -> test
                    === test
                        * Hello[.], world.
                        -> END
                ";

            Story story = CompileString(storyStr);
            story.Continue();

            Assert.That(story.currentChoices[0].text, Is.EqualTo("Hello."));

            story.ChooseChoiceIndex(0);
            Assert.That(story.Continue(), Is.EqualTo("Hello, world.\n"));
        }

        [Test()]
        public void TestWhitespace()
        {
            var storyStr =
@"
-> firstKnot
=== firstKnot
    Hello!
    -> anotherKnot

=== anotherKnot
    World.
    -> END
";

            Story story = CompileString(storyStr);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Hello!\nWorld.\n"));
        }

        [Test()]
        public void TestVisitCountsWhenChoosing()
        {
            var storyStr =
                @"
== TestKnot ==
this is a test
+ [Next] -> TestKnot2

== TestKnot2 ==
this is the end
-> END
";

            Story story = CompileString(storyStr, countAllVisits:true);

            Assert.That(story.state.VisitCountAtPathString("TestKnot"), Is.EqualTo(0));
            Assert.That(story.state.VisitCountAtPathString("TestKnot2"), Is.EqualTo(0));

            story.ChoosePathString ("TestKnot");

            Assert.That(story.state.VisitCountAtPathString("TestKnot"), Is.EqualTo(1));
            Assert.That(story.state.VisitCountAtPathString("TestKnot2"), Is.EqualTo(0));

            story.Continue ();

            Assert.That(story.state.VisitCountAtPathString("TestKnot"), Is.EqualTo(1));
            Assert.That(story.state.VisitCountAtPathString("TestKnot2"), Is.EqualTo(0));

            story.ChooseChoiceIndex (0);

            Assert.That(story.state.VisitCountAtPathString("TestKnot"), Is.EqualTo(1));

            // At this point, we have made the choice, but the divert *within* the choice
            // won't yet have been evaluated.
            Assert.That(story.state.VisitCountAtPathString("TestKnot2"), Is.EqualTo(0));

            story.Continue ();

            Assert.That(story.state.VisitCountAtPathString("TestKnot"), Is.EqualTo(1));
            Assert.That(story.state.VisitCountAtPathString("TestKnot2"), Is.EqualTo(1));
        }

        // https://github.com/inkle/ink/issues/539
        [Test()]
        public void TestVisitCountBugDueToNestedContainers()
        {
            var storyStr = @"
                - (gather) {gather}
                * choice
                - {gather}
            ";

            Story story = CompileString(storyStr);

            Assert.That(story.Continue(), Is.EqualTo("1\n"));

            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("choice\n1\n"));
        }

        [Test()]
        public void TestTempGlobalConflict()
        {
            // Test bug where temp was being treated as a global
            var storyStr =
                @"
-> outer
=== outer
~ temp x = 0
~ f(x)
{x}
-> DONE

=== function f(ref x)
~temp local = 0
~x=x
{setTo3(local)}

=== function setTo3(ref x)
~x = 3
";

            Story story = CompileString(storyStr);

            Assert.That(story.Continue(), Is.EqualTo("0\n"));
        }

        [Test()]
        public void TestThreadInLogic()
        {
            var storyStr =
                @"
-> once ->
-> once ->

== once ==
{<- content|}
->->

== content ==
Content
-> DONE
";

            Story story = CompileString(storyStr);

            Assert.That(story.Continue(), Is.EqualTo("Content\n"));
        }

        [Test ()]
        public void TestTempUsageInOptions ()
        {
            var storyStr =
                @"
~ temp one = 1
* \ {one}
- End of choice 
    -> another
* (another) this [is] another
 -> DONE
";

            Story story = CompileString (storyStr);
            story.Continue ();

            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("1"));
            story.ChooseChoiceIndex (0);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("1\nEnd of choice\nthis another\n"));

            Assert.That(story.currentChoices, Is.Empty);
        }


        [Test ()]
        public void TestEvaluatingInkFunctionsFromGame ()
        {
            var storyStr =
                @"
Top level content
* choice

== somewhere ==
= here
-> DONE

== function test ==
~ return -> somewhere.here
";

            Story story = CompileString (storyStr);
            story.Continue ();

            var returnedDivertTarget = story.EvaluateFunction ("test");

            // Divert target should get returned as a string
            Assert.That(returnedDivertTarget, Is.EqualTo("somewhere.here"));
        }

        [Test ()]
        public void TestEvaluatingInkFunctionsFromGame2 ()
        {
            var storyStr =
                @"
One
Two
Three

== function func1 ==
This is a function
~ return 5

== function func2 ==
This is a function without a return value
~ return

== function add(x,y) ==
x = {x}, y = {y}
~ return x + y
";

            Story story = CompileString (storyStr);

            string textOutput;
            var funcResult = story.EvaluateFunction ("func1", out textOutput);
            Assert.That(textOutput, Is.EqualTo("This is a function\n"));
            Assert.That(funcResult, Is.EqualTo(5));

            Assert.That(story.Continue(), Is.EqualTo("One\n"));

            funcResult = story.EvaluateFunction ("func2", out textOutput);
            Assert.That(textOutput, Is.EqualTo("This is a function without a return value\n"));
            Assert.That(funcResult, Is.Null);

            Assert.That(story.Continue(), Is.EqualTo("Two\n"));

            funcResult = story.EvaluateFunction ("add", out textOutput, 1, 2);
            Assert.That(textOutput, Is.EqualTo("x = 1, y = 2\n"));
            Assert.That(funcResult, Is.EqualTo(3));

            Assert.That(story.Continue(), Is.EqualTo("Three\n"));
        }

        [Test ()]
        public void TestEvaluatingFunctionVariableStateBug ()
        {
            var storyStr =
                @"
Start
-> tunnel ->
End
-> END

== tunnel ==
In tunnel.
->->

=== function function_to_evaluate() ===
    { zero_equals_(1):
        ~ return ""WRONG""
    - else:
        ~ return ""RIGHT""
    }

=== function zero_equals_(k) ===
    ~ do_nothing(0)
    ~ return  (0 == k)

=== function do_nothing(k)
    ~ return 0
";

            Story story = CompileString (storyStr);

            Assert.That(story.Continue(), Is.EqualTo("Start\n"));
            Assert.That(story.Continue(), Is.EqualTo("In tunnel.\n"));

            var funcResult = story.EvaluateFunction ("function_to_evaluate");
            Assert.That(funcResult, Is.EqualTo("RIGHT"));

            Assert.That(story.Continue(), Is.EqualTo("End\n"));
        }

        [Test ()]
        public void TestDoneStopsThread ()
        {
            var storyStr =
                @"
-> DONE
This content is inaccessible.
";

            Story story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.Empty);
        }

        [Test ()]
        public void TestWrongVariableDivertTargetReference ()
        {
            var storyStr =
                @"
-> go_to_broken(-> SOMEWHERE)

== go_to_broken(-> b)
 -> go_to(-> b) // INSTEAD OF: -> go_to(b)

== go_to(-> a)
  -> a

== SOMEWHERE ==
Should be able to get here!
-> DONE
";
            CompileStringWithoutRuntime (storyStr, testingErrors:true);

            Assert.That(HadError("it shouldn't be preceded by '->'"), Is.True);
        }

        [Test ()]
        public void TestLeftRightGlueMatching ()
        {
            var storyStr =
                @"
A line.
{ f():
    Another line.
}

== function f ==
{false:nothing}
~ return true

";
            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("A line.\nAnother line.\n"));
        }

        [Test ()]
        public void TestSetNonExistantVariable ()
        {
            var storyStr =
                @"
VAR x = ""world""
Hello {x}.
";
            var story = CompileString (storyStr);

            Assert.That(story.Continue(), Is.EqualTo("Hello world.\n"));

            Assert.Throws<StoryException>(() => {
                story.variablesState ["y"] = "earth";
            });
        }

        [Test ()]
        public void TestConstRedefinition ()
        {
            var storyStr =
                @"
CONST pi = 3.1415
CONST pi = 3.1415

CONST x = ""Hello""
CONST x = ""World""

CONST y = 3
CONST y = 3.0

CONST z = -> somewhere
CONST z = -> elsewhere

== somewhere ==
-> DONE

== elsewhere ==
-> DONE
";
            CompileStringWithoutRuntime (storyStr, testingErrors:true);

            Assert.That(HadError("'pi' has been redefined"), Is.False);
            Assert.That(HadError("'x' has been redefined"), Is.True);
            Assert.That(HadError("'y' has been redefined"), Is.True);
            Assert.That(HadError("'z' has been redefined"), Is.True);
        }

        [Test ()]
        public void TestTags ()
        {
            var storyStr =
                @"
VAR x = 2 
# author: Joe
# title: My Great Story
This is the content

== knot ==
# knot tag
Knot content
# end of knot tag
-> END

= stitch
# stitch tag
Stitch content
# this tag is below some content so isn't included in the static tags for the stitch
-> END
";
            var story = CompileString (storyStr);

            var globalTags = new List<string> ();
            globalTags.Add ("author: Joe");
            globalTags.Add ("title: My Great Story");

            var knotTags = new List<string> ();
            knotTags.Add ("knot tag");

            var knotTagWhenContinuedTwice = new List<string> ();
            knotTagWhenContinuedTwice.Add ("end of knot tag");

            var stitchTags = new List<string> ();
            stitchTags.Add ("stitch tag");

            Assert.That(story.globalTags, Is.EqualTo(globalTags));
            Assert.That(story.Continue(), Is.EqualTo("This is the content\n"));
            Assert.That(story.currentTags, Is.EqualTo(globalTags));

            Assert.That(story.TagsForContentAtPath("knot"), Is.EqualTo(knotTags));
            Assert.That(story.TagsForContentAtPath("knot.stitch"), Is.EqualTo(stitchTags));

            story.ChoosePathString ("knot");
            Assert.That(story.Continue(), Is.EqualTo("Knot content\n"));
            Assert.That(story.currentTags, Is.EqualTo(knotTags));
            Assert.That(story.Continue(), Is.Empty);
            Assert.That(story.currentTags, Is.EqualTo(knotTagWhenContinuedTwice));
        }

        [Test ()]
        public void TestTunnelOnwardsDivertOverride ()
        {
            var storyStr =
                @"
-> A ->
We will never return to here!

== A ==
This is A
->-> B

== B ==
Now in B.
-> END
";
            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("This is A\nNow in B.\n"));
        }

        [Test ()]
        public void TestListBasicOperations ()
        {
            var storyStr =
                @"
LIST list = a, (b), c, (d), e
{list}
{(a, c) + (b, e)}
{(a, b, c) ^ (c, b, e)}
{list ? (b, d, e)}
{list ? (d, b)}
{list !? (c)}
";
            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("b, d\na, b, c, e\nb, c\nfalse\ntrue\ntrue\n"));
        }


        [Test ()]
        public void TestListMixedItems ()
        {
            var storyStr =
                @"
LIST list = (a), b, (c), d, e
LIST list2 = x, (y), z
{list + list2}
";
            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("a, y, c\n"));
        }


        [Test ()]
        public void TestMoreListOperations ()
        {
            var storyStr =
                @"
LIST list = l, m = 5, n
{LIST_VALUE(l)}

{list(1)}

~ temp t = list()
~ t += n
{t}
~ t = LIST_ALL(t)
~ t -= n
{t}
~ t = LIST_INVERT(t)
{t}
";
            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("1\nl\nn\nl, m\nn\n"));
        }

        [Test ()]
        public void TestEmptyListOrigin ()
        {
            var storyStr =
                @"
LIST list = a, b
{LIST_ALL(list)}

";
            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("a, b\n"));
        }


                [Test ()]
        public void TestContainsEmptyListAlwaysFalse ()
        {
            var storyStr =
                @"
LIST list = (a), b
{list ? ()}
{() ? ()}
{() ? list}
";
            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("false\nfalse\nfalse\n"));
        }


        [Test ()]
        public void TestEmptyListOriginAfterAssignment ()
        {
            var storyStr =
                @"
LIST x = a, b, c
~ x = ()
{LIST_ALL(x)}
";
            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("a, b, c\n"));
        }

        [Test ()]
        public void TestListSaveLoad ()
        {
            var storyStr =
                @"
LIST l1 = (a), b, (c)
LIST l2 = (x), y, z

VAR t = ()
~ t = l1 + l2
{t}

== elsewhere ==
~ t += z
{t}
-> END
";
            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("a, x, c\n"));

            var savedState = story.state.ToJson ();

            // Compile new version of the story
            story = CompileString (storyStr);

            // Load saved game
            story.state.LoadJson (savedState);

            story.ChoosePathString ("elsewhere");
            Assert.That(story.ContinueMaximally(), Is.EqualTo("a, x, c, z\n"));
        }

        [Test ()]
        public void TestEmptyThreadError ()
        {
            CompileStringWithoutRuntime ("<-", testingErrors:true);
            Assert.That(HadError("Expected target for new thread"), Is.True);
        }

        [Test ()]
        public void TestAuthorWarningsInsideContentListBug ()
        {
            var storyStr =
                @"
{ once:
- a
TODO: b
}
";
            CompileString (storyStr, testingErrors:true);
            Assert.That(HadError(), Is.False);
        }

        [Test ()]
        public void TestWeaveWithinSequence ()
        {
            var storyStr =
                @"
{ shuffle:
-   * choice
    nextline
    -> END
}
";
            var story = CompileString (storyStr);

            story.Continue ();

            Assert.That(story.currentChoices.Count == 1, Is.True);

            story.ChooseChoiceIndex (0);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("choice\nnextline\n"));
        }


        [Test()]
        public void TestNestedChoiceError()
        {
            var storyStr =
                @"
{ true:
    * choice
}
";
            CompileString(storyStr, testingErrors:true);
            Assert.That(HadError("need to explicitly divert"), Is.True);
        }


        [Test ()]
        public void TestStitchNamingCollision ()
        {
            var storyStr =
                @"
VAR stitch = 0

== knot ==
= stitch
->DONE
";
            CompileString (storyStr, countAllVisits: false, testingErrors: true);

            Assert.That(HadError("already been used for a var"), Is.True);
        }


        [Test ()]
        public void TestWeavePointNamingCollision ()
        {
            var storyStr =
                @"
-(opts)
opts1
-(opts)
opts1
-> END
";
            CompileString (storyStr, countAllVisits: false, testingErrors:true);

            Assert.That(HadError("with the same label"), Is.True);
        }

        [Test ()]
        public void TestVariableNamingCollisionWithFlow ()
        {
            var storyStr =
                @"
LIST someList = A, B

~temp heldItems = (A) 
{LIST_COUNT (heldItems)}

=== function heldItems ()
~ return (A)
        ";
            CompileString (storyStr, countAllVisits: false, testingErrors: true);

            Assert.That(HadError("name has already been used for a function"), Is.True);
        }

        [Test ()]
        public void TestVariableNamingCollisionWithArg ()
        {
            var storyStr =
                @"=== function knot (a)
                    ~temp a = 1";
            
            CompileString (storyStr, countAllVisits: false, testingErrors: true);

            Assert.That(HadError("has already been used"), Is.True);
        }

        [Test ()]
        public void TestTunnelOnwardsDivertAfterWithArg ()
        {
            var storyStr =
@"
-> a ->  

=== a === 
->-> b (5 + 3)

=== b (x) ===
{x} 
-> END
";

            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("8\n"));
        }

        [Test ()]
        public void TestVariousDefaultChoices ()
        {
            var storyStr =
@"
* -> hello
Unreachable
- (hello) 1
* ->
   - - 2
- 3
-> END
";

            var story = CompileString (storyStr);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("1\n2\n3\n"));
        }


        [Test ()]
        public void TestVariousBlankChoiceWarning ()
        {
        	var storyStr =
        @"
* [] blank
        ";

        	CompileString (storyStr, testingErrors:true);
            Assert.That(HadWarning("Blank choice"), Is.True);
        }

        [Test ()]
        public void TestTunnelOnwardsWithParamDefaultChoice ()
        {
            var storyStr =
@"
-> tunnel ->

== tunnel ==
* ->-> elsewhere (8)

== elsewhere (x) ==
{x}
-> END
";

            var story = CompileString (storyStr);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("8\n"));
        }


        [Test ()]
        public void TestTunnelOnwardsToVariableDivertTarget ()
        {
            var storyStr =
@"
-> outer ->

== outer
This is outer
-> cut_to(-> the_esc)

=== cut_to(-> escape) 
    ->-> escape
    
== the_esc
This is the_esc
-> END
";

            var story = CompileString (storyStr);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("This is outer\nThis is the_esc\n"));
        }


        [Test ()]
        public void TestReadCountVariableTarget ()
        {
            var storyStr =
@"
VAR x = ->knot

Count start: {READ_COUNT (x)} {READ_COUNT (-> knot)} {knot}

-> x (1) ->
-> x (2) ->
-> x (3) ->

Count end: {READ_COUNT (x)} {READ_COUNT (-> knot)} {knot}
-> END


== knot (a) ==
{a}
->->
";

            var story = CompileString (storyStr, countAllVisits:true);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Count start: 0 0 0\n1\n2\n3\nCount end: 3 3 3\n"));
        }


        [Test ()]
        public void TestDivertTargetsWithParameters ()
        {
            var storyStr =
@"
VAR x = ->place

->x (5)

== place (a) ==
{a}
-> DONE
";

            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("5\n"));
        }

        [Test ()]
        public void TestTagsInSeq()
        {
            var storyStr = 
@"
-> knot -> knot ->
== knot
A {red #red|white #white|blue #blue|green #green} sequence.
->->
";
            var story = CompileString (storyStr);

            Assert.That(story.Continue(), Is.EqualTo("A red sequence.\n"));
            Assert.That(story.currentTags, Is.EqualTo(new List<string> { "red" }));

            Assert.That(story.Continue(), Is.EqualTo("A white sequence.\n"));
            Assert.That(story.currentTags, Is.EqualTo(new List<string> { "white" }));
        }

        [Test ()]
        public void TestTagsInChoice()
        {
            var storyStr = @"+ one #one [two #two] three #three -> END";
            var story = CompileString (storyStr);

            story.Continue ();
            Assert.That(story.currentTags, Is.Empty);
            Assert.That(story.currentChoices.Count, Is.EqualTo(1));
            Assert.That(story.currentChoices[0].tags, Is.EqualTo(new List<string> { "one", "two" }));

            story.ChooseChoiceIndex(0);

            Assert.That(story.Continue(), Is.EqualTo("one three"));
            Assert.That(story.currentTags, Is.EqualTo(new List<string> { "one", "three" }));
        }

        [Test ()]
        public void TestTagsDynamicContent()
        {
            var storyStr = @"tag # pic{5+3}{red|blue}.jpg";
            var story = CompileString (storyStr);

            Assert.That(story.Continue(), Is.EqualTo("tag\n"));
            Assert.That(story.currentTags, Is.EqualTo(new List<string> { "pic8red.jpg" }));
        }

        [Test ()]
        public void TestStringContains ()
        {
        	var storyStr =
@"
{""hello world"" ? ""o wo""}
{""hello world"" ? ""something else""}
{""hello"" ? """"}
{"""" ? """"}
";

        	var story = CompileString (storyStr);

        	var result = story.ContinueMaximally ();

            Assert.That(result, Is.EqualTo("true\nfalse\ntrue\ntrue\n"));
        }

        [Test ()]
        public void TestEvaluationStackLeaks ()
        {
        	var storyStr =
@"
{false:
    
- else: 
    else
}

{6:
- 5: five
- else: else
}

-> onceTest ->
-> onceTest ->

== onceTest ==
{once:
- hi
}
->->
";

        	var story = CompileString (storyStr);

        	var result = story.ContinueMaximally ();

            Assert.That(result, Is.EqualTo("else\nelse\nhi\n"));
            Assert.That(story.state.evaluationStack.Count == 0, Is.True);
        }

        [Test ()]
        public void TestGameInkBackAndForth ()
        {
        	var storyStr =
            @"
EXTERNAL gameInc(x)

== function topExternal(x)
In top external
~ return gameInc(x)

== function inkInc(x)
~ return x + 1

            ";

        	var story = CompileString (storyStr);

            // Crazy game/ink callstack:
            // - Game calls "topExternal(5)" (Game -> ink)
            // - topExternal calls gameInc(5) (ink -> Game)
            // - gameInk increments to 6
            // - gameInk calls inkInc(6) (Game -> ink)
            // - inkInc just increments to 7 (ink)
            // And the whole thing unwinds again back to game.

            story.BindExternalFunction("gameInc", (int x) => {
                x++;
                x = (int) story.EvaluateFunction ("inkInc", x);
                return x;
            });

            string strResult;
            var finalResult = (int) story.EvaluateFunction ("topExternal", out strResult, 5);

            Assert.That(finalResult, Is.EqualTo(7));
            Assert.That(strResult, Is.EqualTo("In top external\n"));
        }


        [Test ()]
        public void TestNewlinesWithStringEval ()
        {
        	var storyStr =
@"
A
~temp someTemp = string()
B

A 
{string()}
B

=== function string()    
    ~ return ""{3}""
}
";

        	var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("A\nB\nA\n3\nB\n"));
        }


        [Test ()]
        public void TestNewlinesTrimmingWithFuncExternalFallback ()
        {
        	var storyStr =
@"
EXTERNAL TRUE ()

Phrase 1 
{ TRUE ():

	Phrase 2
}
-> END 

=== function TRUE ()
	~ return true
";

        	var story = CompileString (storyStr);
            story.allowExternalFunctionFallbacks = true;

            Assert.That(story.ContinueMaximally(), Is.EqualTo("Phrase 1\nPhrase 2\n"));
        }

        [Test ()]
        public void TestMultilineLogicWithGlue ()
        {
        	var storyStr =
@"
{true:
    a 
} <> b


{true:
    a 
} <> { true: 
    b 
}
";
        	var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("a b\na b\n"));
        }



        [Test ()]
        public void TestNewlineAtStartOfMultilineConditional ()
        {
        	var storyStr =
        @"
{isTrue():
    x
}

=== function isTrue()
    X
	~ return true
        ";
        	var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("X\nx\n"));
        }

        [Test ()]
        public void TestTempNotFound ()
        {
        	var storyStr =
        @"
{x}
~temp x = 5
hello
                ";
        	var story = CompileString (storyStr, testingErrors:true);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("0\nhello\n"));

            Assert.That(HadWarning(), Is.True);
        }


        [Test ()]
        public void TestTempNotAllowedCrossStitch ()
        {
        	var storyStr =
                @"
-> knot.stitch

== knot (y) ==
~temp x = 5
-> END

= stitch
{x} {y}
-> END
			";
            
        	CompileStringWithoutRuntime (storyStr, testingErrors:true);

            Assert.That(HadError("Unresolved variable: x"), Is.True);
            Assert.That(HadError("Unresolved variable: y"), Is.True);
        }



        [Test ()]
        public void TestTopFlowTerminatorShouldntKillThreadChoices ()
        {
        	var storyStr =
        		@"
<- move
Limes 

=== move
	* boop
        -> END
                    ";

            var story = CompileString (storyStr);

            Assert.That(story.Continue(), Is.EqualTo("Limes\n"));
            Assert.That(story.currentChoices.Count == 1, Is.True);
        }


        [Test ()]
        public void TestNewlineConsistency ()
        {
        	var storyStr =
        		@"
hello -> world
== world
world 
-> END";

        	var story = CompileString (storyStr);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("hello world\n"));

            storyStr =
	@"
* hello -> world
== world
world 
-> END";
            story = CompileString (storyStr);

            story.Continue ();
            story.ChooseChoiceIndex (0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("hello world\n"));


            storyStr =
    @"
* hello 
	-> world
== world
world 
-> END";
            story = CompileString (storyStr);

            story.Continue ();
            story.ChooseChoiceIndex (0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("hello\nworld\n"));
        }


        [Test ()]
        public void TestListRandom ()
        {
            var storyStr =
                @"
LIST l = A, (B), (C), (D), E
{LIST_RANDOM(l)}
{LIST_RANDOM (l)}
{LIST_RANDOM (l)}
{LIST_RANDOM (l)}
{LIST_RANDOM (l)}
{LIST_RANDOM (l)}
{LIST_RANDOM (l)}
{LIST_RANDOM (l)}
{LIST_RANDOM (l)}
{LIST_RANDOM (l)}
                    ";

            var story = CompileString (storyStr);

            while (story.canContinue) {
                var result = story.Continue ();
                Assert.That(result == "B\n" || result == "C\n" || result == "D\n", Is.True);
            }
        }


        [Test ()]
        public void TestTurns ()
        {
            var storyStr =
                @"
-> c
- (top)
+ (c) [choice]
    {TURNS ()}
    -> top
                    ";

            var story = CompileString (storyStr);

            for (int i = 0; i < 10; i++) {
                Assert.That(story.Continue(), Is.EqualTo(i + "\n"));
                story.ChooseChoiceIndex (0);
            }
        }




        [Test ()]
        public void TestLogicLinesWithNewlines ()
        {
            // Both "~" lines should be followed by newlines
            // since func() has a text output side effect.
            var storyStr =
        @"
~ func ()
text 2

~temp tempVar = func ()
text 2

== function func ()
	text1
	~ return true
";

            var story = CompileString (storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("text1\ntext 2\ntext1\ntext 2\n"));
        }

        [Test()]
        public void TestFloorCeilingAndCasts()
        {
            var storyStr =
        @"
{FLOOR(1.2)}
{INT(1.2)}
{CEILING(1.2)}
{CEILING(1.2) / 3}
{INT(CEILING(1.2)) / 3}
{FLOOR(1)}
";

            var story = CompileString(storyStr);

            Assert.That(story.ContinueMaximally(), Is.EqualTo("1\n1\n2\n0.6666667\n0\n1\n"));
        }

        [Test()]
        public void TestListRange()
        {
            var storyStr =
        @"
LIST Food = Pizza, Pasta, Curry, Paella
LIST Currency = Pound, Euro, Dollar
LIST Numbers = One, Two, Three, Four, Five, Six, Seven

VAR all = ()
~ all = LIST_ALL(Food) + LIST_ALL(Currency)
{all}
{LIST_RANGE(all, 2, 3)}
{LIST_RANGE(LIST_ALL(Numbers), Two, Six)}
{LIST_RANGE(LIST_ALL(Numbers), Currency, Three)}
{LIST_RANGE((Pizza, Pasta), -1, 100)} // allow out of range
";

            var story = CompileString(storyStr);

            Assert.That(
story.ContinueMaximally(), Is.EqualTo(@"Pound, Pizza, Euro, Pasta, Dollar, Curry, Paella
Euro, Pasta, Dollar, Curry
Two, Three, Four, Five, Six
One, Two, Three
Pizza, Pasta
".Replace(Environment.NewLine, "\n")));
        }
           
        // Fix for rogue "can't use as sub-expression" bug
        [Test()]
        public void TestUsingFunctionAndIncrementTogether()
        {
            var storyStr =
        @"
VAR x = 5
~ x += one()
    
=== function one()
~ return 1
";
             
            // Ensure it just compiles
            CompileStringWithoutRuntime(storyStr);
        }

        // Fix for rogue "can't use as sub-expression" bug
        [Test()]
        public void TestKnotStitchGatherCounts()
        {
            var storyStr =
        @"
VAR knotCount = 0
VAR stitchCount = 0

-> gather_count_test ->

~ knotCount = 0
-> knot_count_test ->

~ knotCount = 0
-> knot_count_test ->

-> stitch_count_test ->

== gather_count_test ==
VAR gatherCount = 0
- (loop)
~ gatherCount++
{gatherCount} {loop}
{gatherCount<3:->loop}
->->

== knot_count_test ==
~ knotCount++
{knotCount} {knot_count_test}
{knotCount<3:->knot_count_test}
->->


== stitch_count_test ==
~ stitchCount = 0
-> stitch ->
~ stitchCount = 0
-> stitch ->
->->

= stitch
~ stitchCount++
{stitchCount} {stitch}
{stitchCount<3:->stitch}
->->
";

            // Ensure it just compiles
            var story = CompileString(storyStr);

            Assert.That(
story.ContinueMaximally(), Is.EqualTo(@"1 1
2 2
3 3
1 1
2 1
3 1
1 2
2 2
3 2
1 1
2 1
3 1
1 2
2 2
3 2
".Replace(Environment.NewLine, "\n")));
        }

        // Fix for threads being incorrectly reused between choices
        // and the main thread after save/reload
        // https://github.com/inkle/ink/issues/463
        [Test()]
        public void TestChoiceThreadForking()
        {
            var storyStr =
        @"
-> generate_choice(1) ->

== generate_choice(x) ==
{true:
    + A choice
        Vaue of local var is: {x}
        -> END
}
->->
";

            // Generate the choice with the forked thread
            var story = CompileString(storyStr);
            story.Continue();

            // Save/reload
            var savedState = story.state.ToJson();
            story = CompileString(storyStr);
            story.state.LoadJson(savedState);

            // Load the choice, it should have its own thread still
            // that still has the captured temp x
            story.ChooseChoiceIndex(0);
            story.ContinueMaximally();

            // Don't want this warning:
            // RUNTIME WARNING: '' line 7: Variable not found: 'x'
            Assert.That(story.hasWarning, Is.False);
        }


        [Test()]
        public void TestFallbackChoiceOnThread()
        {
            var storyStr =
        @"
<- knot

== knot
   ~ temp x = 1
   *   ->
       Should be 1 not 0: {x}.
       -> DONE
";

            var story = CompileString(storyStr);
            Assert.That(story.Continue(), Is.EqualTo("Should be 1 not 0: 1.\n"));
        }

        // Test for bug where after a call to ChoosePathString,
        // the callstack is not fully/cleanly reset, e.g. leaving
        // "inExpressionEvaluation" variable left to true, as set during
        // the call to {RunAThing()}.
        // This was when we unwound the callstack, but we didn't reset
        // the base element.
        [Test()]
        public void TestCleanCallstackResetOnPathChoice()
        {
            var storyStr =
        @"
{RunAThing()}

== function RunAThing ==
The first line.
The second line.

== SomewhereElse ==
{""somewhere else""}
->END
";

            var story = CompileString(storyStr);

            Assert.That(story.Continue(), Is.EqualTo("The first line.\n"));

            story.ChoosePathString("SomewhereElse");

            Assert.That(story.ContinueMaximally(), Is.EqualTo("somewhere else\n"));
        }


        // Test for bug where choice's owned thread would get 
        // reused between re-runs after a state reset, and in
        // this case would be in the middle of expression evaluation
        // at the time, causing an error.
        // Fixed by re-forking the choice thread
        // in TryFollowDefaultInvisibleChoice
        [Test()]
        public void TestStateRollbackOverDefaultChoice()
        {
            var storyStr =
        @"
<- make_default_choice
Text.

=== make_default_choice
    *   -> 
        {5}
        -> END 
";

            var story = CompileString(storyStr);

            Assert.That(story.Continue(), Is.EqualTo("Text.\n"));
            Assert.That(story.Continue(), Is.EqualTo("5\n"));
        }

        // Bools used to be represented purely
        // using ints. However as of September 2020
        // we decided to add "proper" bools but with the
        // original semantics that made the int-based bools
        // useful - i.e. the ability to easily upgrade
        // a simple bool flag to a counter. Therefore we
        // get the slightly nausia-inducing "true + 1 == 2",
        // "1 == true", etc. It's for the best though, I promise!
        [Test()]
        public void TestBools()
        {
            Assert.That(CompileString("{true}").Continue(), Is.EqualTo("true\n"));
            Assert.That(CompileString("{true + 1}").Continue(), Is.EqualTo("2\n"));
            Assert.That(CompileString("{2 + true}").Continue(), Is.EqualTo("3\n"));
            Assert.That(CompileString("{false + false}").Continue(), Is.EqualTo("0\n"));
            Assert.That(CompileString("{true + true}").Continue(), Is.EqualTo("2\n"));
            Assert.That(CompileString("{true == 1}").Continue(), Is.EqualTo("true\n"));
            Assert.That(CompileString("{not 1}").Continue(), Is.EqualTo("false\n"));
            Assert.That(CompileString("{not true}").Continue(), Is.EqualTo("false\n"));
            Assert.That(CompileString("{3 > 1}").Continue(), Is.EqualTo("true\n"));

            var listHasntStory = @"
                LIST list = a, (b), c, (d), e
                {list !? (c)}
            ";
            Assert.That(CompileString(listHasntStory).Continue(), Is.EqualTo("true\n"));
        }


        [Test()]
        public void TestMultiFlowBasics()
        {
            var storyStr =
        @"
=== knot1
knot 1 line 1
knot 1 line 2
-> END 

=== knot2
knot 2 line 1
knot 2 line 2
-> END 
";

            var story = CompileString(storyStr);

            story.SwitchFlow("First");
            story.ChoosePathString("knot1");
            Assert.That(story.Continue(), Is.EqualTo("knot 1 line 1\n"));

            story.SwitchFlow("Second");
            story.ChoosePathString("knot2");
            Assert.That(story.Continue(), Is.EqualTo("knot 2 line 1\n"));

            story.SwitchFlow("First");
            Assert.That(story.Continue(), Is.EqualTo("knot 1 line 2\n"));

            story.SwitchFlow("Second");
            Assert.That(story.Continue(), Is.EqualTo("knot 2 line 2\n"));
        }

        [Test()]
        public void TestMultiFlowSaveLoadThreads()
        {
            var storyStr =
        @"
Default line 1
Default line 2

== red ==
Hello I'm red
<- thread1(""red"")
<- thread2(""red"")
-> DONE

== blue ==
Hello I'm blue
<- thread1(""blue"")
<- thread2(""blue"")
-> DONE

== thread1(name) ==
+ Thread 1 {name} choice
    -> thread1Choice(name)

== thread2(name) ==
+ Thread 2 {name} choice
    -> thread2Choice(name)

== thread1Choice(name) ==
After thread 1 choice ({name})
-> END

== thread2Choice(name) ==
After thread 2 choice ({name})
-> END
";

            var story = CompileString(storyStr);

            // Default flow
            Assert.That(story.Continue(), Is.EqualTo("Default line 1\n"));

            story.SwitchFlow("Blue Flow");
            story.ChoosePathString("blue");
            Assert.That(story.Continue(), Is.EqualTo("Hello I'm blue\n"));

            story.SwitchFlow("Red Flow");
            story.ChoosePathString("red");
            Assert.That(story.Continue(), Is.EqualTo("Hello I'm red\n"));

            // Test existing state remains after switch (blue)
            story.SwitchFlow("Blue Flow");
            Assert.That(story.currentText, Is.EqualTo("Hello I'm blue\n"));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("Thread 1 blue choice"));

            // Test existing state remains after switch (red)
            story.SwitchFlow("Red Flow");
            Assert.That(story.currentText, Is.EqualTo("Hello I'm red\n"));
            Assert.That(story.currentChoices[0].text, Is.EqualTo("Thread 1 red choice"));

            // Save/load test
            var saved = story.state.ToJson();
            
            // Test choice before reloading state before resetting
            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Thread 1 red choice\nAfter thread 1 choice (red)\n"));
            story.ResetState();

            // Load to pre-choice: still red, choose second choice
            story.state.LoadJson(saved);

            story.ChooseChoiceIndex(1);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Thread 2 red choice\nAfter thread 2 choice (red)\n"));

            
            // Load: switch to blue, choose 1
            story.state.LoadJson(saved);
            story.SwitchFlow("Blue Flow");
            story.ChooseChoiceIndex(0);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Thread 1 blue choice\nAfter thread 1 choice (blue)\n"));

            // Load: switch to blue, choose 2
            story.state.LoadJson(saved);
            story.SwitchFlow("Blue Flow");
            story.ChooseChoiceIndex(1);
            Assert.That(story.ContinueMaximally(), Is.EqualTo("Thread 2 blue choice\nAfter thread 2 choice (blue)\n"));

            // Remove active blue flow, should revert back to global flow
            story.RemoveFlow("Blue Flow");
            Assert.That(story.Continue(), Is.EqualTo("Default line 2\n"));
        }

        // Helper compile function
        protected Story CompileString(string str, bool countAllVisits = false, bool testingErrors = false)
        {
            _testingErrors = testingErrors;
            _errorMessages.Clear();
            _warningMessages.Clear();
            _authorMessages.Clear ();

            InkParser parser = new InkParser(str, null, OnError);
            var parsedStory = parser.Parse();
            parsedStory.countAllVisits = countAllVisits;

            Story story = parsedStory.ExportRuntime(OnError);
            if ( !testingErrors )
                Assert.That(story, Is.Not.Null);

            if (story != null)
            {
                story.onError += OnError;

                // Convert to json and back again
                if (_mode == TestMode.JsonRoundTrip)
                {
                    var jsonStr = story.ToJson();
                    story = new Story(jsonStr);
                    story.onError += OnError;
                }
            }

            return story;
        }

        protected Ink.Parsed.Story CompileStringWithoutRuntime(string str, bool testingErrors = false)
        {
            _testingErrors = testingErrors;
            _errorMessages.Clear();
            _warningMessages.Clear();
            _authorMessages.Clear ();

            InkParser parser = new InkParser(str, null, OnError);
            var parsedStory = parser.Parse();

            if (!testingErrors) {
                Assert.That(parsedStory, Is.Not.Null);
            }

            if (parsedStory && _errorMessages.Count == 0) {
                parsedStory.ExportRuntime (OnError);
            }

            return parsedStory;
        }

        private bool HadError(string matchStr = null)
        {
            return HadErrorOrWarning(matchStr, _errorMessages);
        }

        private bool HadErrorOrWarning(string matchStr, List<string> list)
        {
            if (matchStr == null)
                return list.Count > 0;

            foreach (var str in list)
            {
                if (str.Contains(matchStr))
                    return true;
            }
            return false;
        }

        private bool HadWarning(string matchStr = null)
        {
            return HadErrorOrWarning(matchStr, _warningMessages);
        }

        private void OnError(string message, ErrorType errorType)
        {
            if (_testingErrors)
            {
                if (errorType == ErrorType.Error)
                    _errorMessages.Add (message);
                else if (errorType == ErrorType.Warning)
                    _warningMessages.Add (message);
                else
                    _authorMessages.Add (message);
            }
            else
                Assert.Fail(message);
        }

        private string GenerateIdentifierFromCharacterRange(CharacterRange range, string varNameUniquePart)
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(varNameUniquePart)) {
                sb.Append(varNameUniquePart);
            }

            CharacterSet charset = range.ToCharacterSet();

            foreach (var c in charset) {
                sb.Append(c);
            }

            return sb.ToString();
        }
        private string GenerateIdentifierFromCharacterRange(CharacterRange range)
        {
            return GenerateIdentifierFromCharacterRange(range, null);
        }


        [Test()]
        public void TestCharacterRangeIdentifiersForConstNamesWithAsciiPrefix()
        {
            var ranges = InkParser.ListAllCharacterRanges();
            for (int i = 0; i < ranges.Length; i++)
            {

                var range = ranges[i];

                var identifier = GenerateIdentifierFromCharacterRange(range);

                var storyStr = string.Format(@"
CONST pi{0} = 3.1415
CONST a{0} = ""World""
CONST b{0} = 3
", identifier);

                var compiledStory = CompileStringWithoutRuntime(storyStr);

                Assert.That(compiledStory, Is.Not.Null);
            }
        }
        [Test()]
        public void TestCharacterRangeIdentifiersForConstNamesWithAsciiSuffix()
        {
            var ranges = InkParser.ListAllCharacterRanges();
            for (int i = 0; i < ranges.Length; i++)
            {

                var range = ranges[i];

                var identifier = GenerateIdentifierFromCharacterRange(range);

                var storyStr = string.Format(@"
CONST {0}pi = 3.1415
CONST {0}a = ""World""
CONST {0}b = 3
", identifier);

                var compiledStory = CompileStringWithoutRuntime(storyStr);

                Assert.That(compiledStory, Is.Not.Null);
            }
        }

        [Test()]
        public void TestCharacterRangeIdentifiersForSimpleVariableNamesWithAsciiPrefix()
        {
            var ranges = InkParser.ListAllCharacterRanges();
            for (int i = 0; i < ranges.Length; i++)
            {

                var range = ranges[i];

                var identifier = GenerateIdentifierFromCharacterRange(range);

                var storyStr = string.Format(@"
VAR pi{0} = 3.1415
VAR a{0} = ""World""
VAR b{0} = 3
", identifier);

                var compiledStory = CompileStringWithoutRuntime(storyStr);

                Assert.That(compiledStory, Is.Not.Null);
            }
        }

        [Test()]
        public void TestCharacterRangeIdentifiersForSimpleVariableNamesWithAsciiSuffix()
        {
            var ranges = InkParser.ListAllCharacterRanges();
            for (int i = 0; i < ranges.Length; i++)
            {

                var range = ranges[i];

                var identifier = GenerateIdentifierFromCharacterRange(range);

                var storyStr = string.Format(@"
VAR {0}pi = 3.1415
VAR {0}a = ""World""
VAR {0}b = 3
", identifier);

                var compiledStory = CompileStringWithoutRuntime(storyStr);

                Assert.That(compiledStory, Is.Not.Null);
            }
        }


        [Test ()]
        public void TestCharacterRangeIdentifiersForDivertNamesWithAsciiPrefix()
        {
            var ranges = InkParser.ListAllCharacterRanges();
            for (int i = 0; i < ranges.Length; i++) {

                var range = ranges[i];
                var rangeString = GenerateIdentifierFromCharacterRange(range);


                var storyStr = string.Format(@"
VAR z{0} = -> divert{0}

== divert{0} ==
-> END
", rangeString);
                
                var compiledStory = CompileStringWithoutRuntime (storyStr);

                Assert.That(compiledStory, Is.Not.Null);
            }
        }
        [Test()]
        public void TestCharacterRangeIdentifiersForDivertNamesWithAsciiSuffix()
        {
            var ranges = InkParser.ListAllCharacterRanges();
            for (int i = 0; i < ranges.Length; i++)
            {

                var range = ranges[i];
                var rangeString = GenerateIdentifierFromCharacterRange(range);


                var storyStr = string.Format(@"
VAR {0}z = -> {0}divert

== {0}divert ==
-> END
", rangeString);

                var compiledStory = CompileStringWithoutRuntime(storyStr);

                Assert.That(compiledStory, Is.Not.Null);
            }
        }

        private class TestWarningException : System.Exception
        { }
    }
}
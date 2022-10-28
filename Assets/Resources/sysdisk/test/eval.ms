import "qa";

ctx = {
    "a": 1,
    "b": 2,
};
cFunc = eval.compileLine("a + b").bindOuters(ctx);
qa.assertEqual cFunc(), 3;

c2Func = eval.compile("c = cFunc; return c;").bindOuters(locals);
qa.assertEqual c2Func(), 3;

print "eval tests done!"
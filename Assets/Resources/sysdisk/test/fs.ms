import "qa";

defaultContent = ["maya", "hee"];

testFile = function(path, content)
    if content == null then
        content = defaultContent
    end if
    file.writeLines(path, content);
    readContent = file.readLines(path);
    qa.assert(readContent != null, "Can't read file");
    qa.assertEqual(content.join("\n"), readContent.join("\n"));

    print "tests on file at '" + path + "' passed successfully"
end function

testFile("/home/maya.txt");

file.makedir("/home/2/");
file.mount("/home/2/", "home2");

testFile("/home2/maya.txt");
testFile("/home2/3/maya.txt");
print file.children("/home2/");
print file.child("/home","2/3");

print("FS tests done!");

TeamCityGraph
=============

Create a graph of TeamCity projects and NuGet packages.

To use:

1. Edit Program.cs and enter your username, password, and TeamCity REST API URI into the fields at the top of the class (yeah yeah, I accept pull requests :P).
2. Build and run, piping the output to a .dot file
3. Run your .dot file through GraphViz to produce the graph in the format of your choice.

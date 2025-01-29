Imports System
Imports System.IO
Imports System.Xml.Linq
Imports System.Collections.Generic
Imports System.Linq

Module Program

    ' Represents an ellipse node in the diagram.
    Class DiagramNode
        Public Property Id As String           ' The <mxCell> id
        Public Property TextValue As String    ' The label in 'value' attribute
        Public Property Children As New List(Of String)()
    End Class

    ' Represents an edge in the diagram (<mxCell edge="1">).
    Class DiagramEdge
        Public Property Id As String           ' The <mxCell> id
        Public Property Source As String
        Public Property Target As String
    End Class

    Sub Main()
        ' Prompt user for draw.io file path
        Console.Write("Enter path to .drawio file (uncompressed): ")
        Dim drawioPath As String = Console.ReadLine().Trim()
        If Not File.Exists(drawioPath) Then
            Console.WriteLine("File not found: " & drawioPath)
            Return
        End If

        ' Prompt user for output directory
        Console.Write("Enter output directory for SQL file: ")
        Dim outputDir As String = Console.ReadLine().Trim()
        If Not Directory.Exists(outputDir) Then
            Try
                Directory.CreateDirectory(outputDir)
                Console.WriteLine("Created directory: " & outputDir)
            Catch ex As Exception
                Console.WriteLine("Could not create directory: " & ex.Message)
                Return
            End Try
        End If

        ' Prompt user for output filename
        Console.Write("Enter output SQL filename [default: output_inserts.sql]: ")
        Dim outputFilename As String = Console.ReadLine().Trim()
        If String.IsNullOrEmpty(outputFilename) Then
            outputFilename = "output_inserts.sql"
        End If

        Dim outputPath As String = Path.Combine(outputDir, outputFilename)

        ' Parse the .drawio file and build SQL
        ParseDrawioToSql_ChainApproach(drawioPath, outputPath)
    End Sub

    Private Sub ParseDrawioToSql_ChainApproach(drawioPath As String, outputPath As String)
        ' 1) Load the <mxfile> root
        Dim doc As XDocument
        Try
            doc = XDocument.Load(drawioPath)
        Catch ex As Exception
            Console.WriteLine("Error reading/parsing the .drawio file: " & ex.Message)
            Return
        End Try

        Dim mxFileElem As XElement = doc.Root
        If mxFileElem Is Nothing OrElse mxFileElem.Name.LocalName <> "mxfile" Then
            Console.WriteLine("Top-level element is not <mxfile>. Check if file is uncompressed .drawio.")
            Return
        End If

        ' 2) Get all <diagram> elements
        Dim diagrams = mxFileElem.Elements("diagram").ToList()
        If diagrams.Count = 0 Then
            Console.WriteLine("No <diagram> elements found under <mxfile>.")
            Return
        End If

        Console.WriteLine("Found {0} diagram(s):", diagrams.Count)
        For i = 0 To diagrams.Count - 1
            Dim name = diagrams(i).Attribute("name")?.Value
            Console.WriteLine("  {0}. '{1}'", i + 1, name)
        Next

        ' Prompt user to pick which diagram to process
        Dim chosenIndex As Integer
        If diagrams.Count = 1 Then
            chosenIndex = 0
        Else
            Do
                Console.Write("Enter the number of the diagram to process: ")
                Dim input = Console.ReadLine()
                If Integer.TryParse(input, chosenIndex) AndAlso chosenIndex >= 1 AndAlso chosenIndex <= diagrams.Count Then
                    chosenIndex -= 1
                    Exit Do
                End If
                Console.WriteLine("Invalid choice. Try again.")
            Loop
        End If

        Dim chosenDiagram = diagrams(chosenIndex)
        Dim mxGraphModelElem = chosenDiagram.Element("mxGraphModel")
        If mxGraphModelElem Is Nothing Then
            Console.WriteLine("Could not find <mxGraphModel> in the chosen diagram.")
            Return
        End If

        Dim rootElem = mxGraphModelElem.Element("root")
        If rootElem Is Nothing Then
            Console.WriteLine("Could not find <root> under <mxGraphModel>.")
            Return
        End If

        ' 3) Store ellipse nodes and edges separately
        Dim nodesById As New Dictionary(Of String, DiagramNode)()
        Dim edgesById As New Dictionary(Of String, DiagramEdge)()

        For Each cell In rootElem.Elements("mxCell")
            Dim cellId = cell.Attribute("id")?.Value
            Dim style = cell.Attribute("style")?.Value
            Dim valueAttr = cell.Attribute("value")
            Dim value = If(valueAttr IsNot Nothing AndAlso valueAttr.Value IsNot Nothing, valueAttr.Value.Trim(), String.Empty)
            Dim edgeFlag = cell.Attribute("edge")?.Value
            Dim vertexFlag = cell.Attribute("vertex")?.Value

            If Not String.IsNullOrEmpty(style) AndAlso style.Contains("ellipse") AndAlso vertexFlag = "1" Then
                ' This is an ellipse node
                Dim dn As New DiagramNode With {
                    .Id = cellId,
                    .TextValue = value
                }
                nodesById(cellId) = dn

            ElseIf edgeFlag = "1" Then
                ' This is an edge
                Dim source = cell.Attribute("source")?.Value
                Dim target = cell.Attribute("target")?.Value
                If Not String.IsNullOrEmpty(source) AndAlso Not String.IsNullOrEmpty(target) Then
                    Dim de As New DiagramEdge With {
                        .Id = cellId,
                        .Source = source,
                        .Target = target
                    }
                    edgesById(cellId) = de
                End If
            End If
        Next

        ' 4) Build adjacency using chain approach:
        '    For each node, find all edges where edge.source == node.Id.
        '    Then chase edge.target if it is an edge, until we reach a node.
        For Each nodeId In nodesById.Keys
            Dim children = FindChildrenViaChain(nodeId, edgesById, nodesById)
            nodesById(nodeId).Children.AddRange(children)
        Next

        ' 5) Prompt user for the root node label and starting index
        Console.Write("Enter the label text of the starting (root) node: ")
        Dim rootText = Console.ReadLine().Trim()

        Console.Write("Enter the start index: ")
        Dim start = Console.ReadLine().Trim()

        ' Find the node ID whose text matches rootText
        Dim rootNodeId As String = nodesById.Values.
            FirstOrDefault(Function(n) n.TextValue = rootText)?.Id

        If String.IsNullOrEmpty(rootNodeId) Then
            Console.WriteLine("No node found with text '{0}'. Aborting.", rootText)
            Return
        End If

        ' 6) DFS from the root to generate SQL inserts
        Dim sqlRows As New List(Of String)()
        Dim autoIncId = start
        Dim stack As New Stack(Of Tuple(Of String, Integer?))()
        stack.Push(Tuple.Create(rootNodeId, CType(Nothing, Integer?)))

        While stack.Count > 0
            Dim current = stack.Pop()
            Dim currentId = current.Item1
            Dim parentId = current.Item2

            Dim currentNode = nodesById(currentId)
            Dim nodeDbId = autoIncId
            autoIncId += 1

            ' If it has no children => end node, else question node
            Dim question = currentNode.TextValue
            Dim endText = If(currentNode.Children.Count = 0, "END_NODE", Nothing)

            Dim parentStr = If(parentId.HasValue, parentId.Value.ToString(), "NULL")
            Dim questionStr = If(question IsNot Nothing, "'" & question.Replace("'", "''") & "'", "NULL")
            Dim endTextStr = If(endText IsNot Nothing, "'" & endText.Replace("'", "''") & "'", "NULL")

            Dim insertLine = $"INSERT INTO Troubleshooting (NodeId, ParentId, Question, EndText) VALUES ({nodeDbId}, {parentStr}, {questionStr}, {endTextStr});"
            sqlRows.Add(insertLine)

            ' Add children to the stack
            For Each childId In currentNode.Children
                stack.Push(Tuple.Create(childId, CType(nodeDbId, Integer?)))
            Next
        End While

        ' 7) Write SQL to file
        Try
            Using sw As New StreamWriter(outputPath, False)
                For Each row In sqlRows
                    sw.WriteLine(row)
                Next
            End Using
            Console.WriteLine("SQL file written to: " & outputPath)
        Catch ex As Exception
            Console.WriteLine("Error writing to SQL file: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' For the given nodeId, finds all child node IDs by following the chain
    ''' of edges from node -> edge -> edge -> node, possibly skipping multiple
    ''' edges in sequence.
    ''' </summary>
    Private Function FindChildrenViaChain(nodeId As String,
                                          edgesDict As Dictionary(Of String, DiagramEdge),
                                          nodesDict As Dictionary(Of String, DiagramNode)) As List(Of String)

        Dim childNodeIds As New List(Of String)()

        ' 1) Find all edges for which edge.source == nodeId
        '    That might be direct or might lead to another edge
        Dim edgesLeadingOut = edgesDict.Values.Where(Function(e) e.Target = nodeId).ToList()

        For Each e In edgesLeadingOut
            Dim finalTarget As String = e.Source

            ' 2) If finalTarget is itself an edge, keep following
            While edgesDict.ContainsKey(finalTarget)
                finalTarget = edgesDict(finalTarget).Source
            End While

            ' 3) Now finalTarget should be the ID of a node (if well-formed)
            If nodesDict.ContainsKey(finalTarget) Then
                childNodeIds.Add(finalTarget)
            End If
        Next

        Return childNodeIds
    End Function

End Module

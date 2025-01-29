# DrawIOToSQL

DrawIOToSQL is a Visual Basic .NET application that parses uncompressed .drawio files and generates SQL insert statements based on the diagram structure. This tool is particularly useful for converting diagrammatic representations into database entries for troubleshooting or other purposes.

## Features

- **Parse .drawio Files**: Reads and parses uncompressed .drawio files to extract diagram elements.
- **Diagram Selection**: Supports multiple diagrams within a single .drawio file and allows the user to select which diagram to process.
- **Node and Edge Extraction**: Identifies and separates ellipse nodes and edges from the diagram.
- **Adjacency Building**: Constructs adjacency relationships using a chain approach to follow edges and build node hierarchies.
- **SQL Generation**: Generates SQL insert statements for nodes and their relationships, ready to be imported into a database.
- **User Prompts**: Interactive prompts for file paths, output directories, and diagram selection.

## Usage

1. **Run the Application**: Execute the application and follow the prompts to provide the path to your .drawio file and the output directory for the SQL file.
2. **Select Diagram**: If the .drawio file contains multiple diagrams, select the one you want to process.
3. **Provide Root Node and Start Index**: Enter the label text of the starting (root) node and the starting index for the SQL inserts.
4. **Generate SQL**: The application will parse the diagram, build the node hierarchy, and generate the SQL insert statements, saving them to the specified output file.

### Example

```
Enter path to .drawio file (uncompressed): C:\path\to\your\diagram.drawio
Enter output directory for SQL file: C:\path\to\output\directory
Enter output SQL filename [default: output_inserts.sql]: my_inserts.sql
Found 2 diagram(s):
  1. 'Diagram 1'
  2. 'Diagram 2'
Enter the number of the diagram to process: 1
Enter the label text of the starting (root) node: RootNodeLabel
Enter the start index: 1
SQL file written to: C:\path\to\output\directory\my_inserts.sql
```

## Output Table Structure

The generated SQL insert statements are designed to populate a table named `Troubleshooting` with the following structure:

| Column   | Type | Description                          |
|----------|------|--------------------------------------|
| NodeId   | INT  | Auto-incremented ID for each node    |
| ParentId | INT  | ID of the parent node (NULL for root nodes) |
| Question | TEXT | The label text of the node           |
| EndText  | TEXT | 'END_NODE' for leaf nodes, NULL for others |

## Requirements

- .NET Framework
- Visual Studio 2022

## Installation

1. **Clone the repository**:
    ```sh
    git clone https://github.com/yourusername/DrawIOToSQL.git
    ```
2. **Open the solution in Visual Studio 2022**.
3. **Build and run the project**.

## Contributing

Contributions are welcome! Please fork the repository and submit pull requests for any enhancements or bug fixes.

## License

This project is licensed under the MIT License.

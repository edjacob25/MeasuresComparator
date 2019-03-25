import os
import argparse

def modify_file(filename):
    print(f"File to clean: {filename}")
    attributes = []
    data = []
    attributes_used = []
    relation_name = ""
    with open(filename) as file:
        relation_name = file.readline()
        data_processing = False
        index = 0
        for line in file:
            if (not data_processing):
                line_upper = line.upper()
                if line_upper.startswith("@ATTRIBUTE"):
                    attribute = line.split(' ')
                    name = attribute[1].rstrip()
                    att_type = ""
                    if len(attribute) > 2:
                        att_type = attribute[2].rstrip()
                    attributes.append((name, att_type, line, index))
                    index += 1
                    continue
                if line_upper.startswith("@INPUTS"):
                    inputs = [x.lstrip() for x in line.split(" ", 1)[1].split(',')]
                    attributes_used = [x.rstrip() for x in inputs]
                    continue
                if line_upper.startswith("@OUTPUT"):
                    outputs = [x.lstrip() for x in line.split(" ", 1)[1].split(',')]
                    attributes_used.extend([x.rstrip() for x in outputs])
                if line_upper.startswith("@DATA"):
                    data_processing = True
            else:
                line_data = [x.lstrip() for x in line.split(',')]
                data.append(line_data)

    new_filename = filename.replace(".dat", "_cleaned.arff")

    with open(new_filename, "w") as new_file:
        new_file.write(relation_name)
        permitted_attributes = [x for x in attributes if x[1] == "" and x[0].split('{')[0] in attributes_used]
        permitted_indexes = [x[3] for x in permitted_attributes]
        for item in permitted_attributes:
            new_file.write(item[2].replace("{", " {" ))
        new_file.write("@DATA\n")
        for datapoint in data:
            points = [x for i, x in enumerate(datapoint) if i in permitted_indexes]
            result = ",".join(points)
            new_file.write(result)
    print(f"Created file {new_filename}")


def apply_cleaning_recursively(root_dir):
    root_dir = os.path.abspath(root_dir)

    for item in os.listdir(root_dir):
        item_full_path = os.path.join(root_dir, item)
        if os.path.isdir(item_full_path):
            apply_cleaning_recursively(item_full_path)
        elif item_full_path.endswith(".dat") or item_full_path.endswith(".dat"):
            modify_file(item_full_path)


parser = argparse.ArgumentParser(description='cleans a dataset or set or datasets to only contain categorical attributes')
parser.add_argument('file', help="File or directory to be converted")

args = parser.parse_args()

if os.path.isdir(args.file):
    print(f"{args.file} is a directory, looking for .dat files inside")
    apply_cleaning_recursively(args.file)
else:
    modify_file(args.file)

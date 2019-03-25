import os

def modify_file(filename):
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
                line_data = line.split(',')
                data.append(line_data)

    new_filename = filename.replace(".dat", "_cleaned.arff")

    with open(new_filename, "w") as new_file:
        new_file.write(relation_name)
        permitted_attributes = [x for x in attributes if x[1] == "" and x[0].split('{')[0] in attributes_used]
        permitted_indexes = [x[3] for x in permitted_attributes]
        for item in permitted_attributes:
            new_file.write(item[2])
        for datapoint in data:
            points = [x for i, x in enumerate(datapoint) if i in permitted_indexes] 
            result = ",".join(points)
            new_file.write(result)

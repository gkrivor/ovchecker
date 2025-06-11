import re
import sys
from pathlib import Path
from PIL import Image, ImageDraw

def parse_file(file_name, output_file):
    allocs = {'usm_host':{'total':0, 'max':0}, 'usm_device':{'total':0, 'max':0}}
    steps = []
    
    w = 720
    h = 576
    
    input_file = open(file_name, 'r', encoding="ascii", errors="ignore")
    for line in input_file:
    #Allocate 73728 bytes of usm_host allocation type ptr = 000001E7C1560000 (current=287002; max=287002)
        m = re.search(r"Allocate ([0-9]+).*(usm_[a-z]+)[^0-9A-F]*([0-9A-F]+)", line)
        if not m is None:
            alloc = int(m.group(1))
            allocs[m.group(2)][m.group(3)] = alloc
            allocs[m.group(2)]['total'] = alloc + allocs[m.group(2)]['total']
            steps.append([m.group(2), allocs[m.group(2)]['total']])
            if allocs[m.group(2)]['total'] > allocs[m.group(2)]['max']:
                allocs[m.group(2)]['max'] = allocs[m.group(2)]['total']
            continue
    #GPU_Debug: memory.cpp:38:~MemoryTracker: Free 64 bytes of usm_host allocation type ptr = 000001E7C37C0000 (current=210594; max=13133082)
        m = re.search(r"Free ([0-9]+).*(usm_[a-z]+)[^0-9A-F]*([0-9A-F]+)", line)
        if not m is None:
            alloc = int(m.group(1))
            if not m.group(2) in allocs:
                print(f"Device isn't found: {m.group(2)}")
                continue
            if not m.group(3) in allocs[m.group(2)]:
                print(f"Allocation of {m.group(3)} isn't found for device {m.group(2)}")
                continue
            if alloc != allocs[m.group(2)][m.group(3)]:
                print(f"Allocation of {m.group(3)} has different size than original found for device {m.group(2)}: {alloc} != {allocs[m.group(2)][m.group(3)]}")
            allocs[m.group(2)].pop(m.group(3))
            allocs[m.group(2)]['total'] = allocs[m.group(2)]['total'] - alloc
            steps.append([m.group(2), allocs[m.group(2)]['total']])
            continue
        if line.startswith(">>> Checkpoint:"):
            steps.append(['cp', line[16:]])
            continue
    input_file.close()

    steps_ppx = (len(steps) + w - 1) // w
    x = -1
    max = 1
    curmem = {}
    for item in allocs.keys():
        if allocs[item]['max'] > max: max = allocs[item]['max']
        curmem[item] = 0
    
    graph = Image.new("RGB", (w,h), (255,255,255))
    drawing = ImageDraw.Draw(graph)
    colors = {'usm_host':'#000C7B', 'usm_device':'#377D22'}

    checkpoints = []
    for i in range(0, len(steps)):
        if steps[i][0] == 'cp':
            drawing.line([(x,h),(x,0)], fill='#ff0000')
        if i % steps_ppx == 0:
            if i != 0:
                print(curmem)
                if curmem['usm_host'] > curmem['usm_device']:
                    drawing.line([(x,h),(x,h - curmem['usm_host']*h/max)], fill=colors['usm_host'])
                    drawing.line([(x,h),(x,h - curmem['usm_device']*h/max)], fill=colors['usm_device'])
                else:
                    drawing.line([(x,h),(x,h - curmem['usm_device']*h/max)], fill=colors['usm_device'])
                    drawing.line([(x,h),(x,h - curmem['usm_host']*h/max)], fill=colors['usm_host'])
            x += 1
        curmem[steps[i][0]] = steps[i][1]

    y = 0
    for item in allocs.keys():
        txt = f"{item} max: {allocs[item]['max']/(1024*1024):.2f}Mb"
        txt_w = drawing.textlength(txt) + 5
        drawing.text([w - txt_w + 1, y+1], txt, '#ffffff')
        drawing.text([w - txt_w, y], txt, colors[item])
        y += 10
    
    graph.save(output_file, "JPEG")

if __name__ == "__main__":
    file_path = None
    if len(sys.argv) > 1:
        file_path = Path(sys.argv[1])
    else:
        file_path = Path("./latest.log")
    output_file = sys.stdout
    if len(sys.argv) > 2:
        if sys.argv[2] != 'stdout':
            output_file = Path(sys.argv[2])
    else:
        output_file = Path("./gpu_mem_pool.jpg")
    parse_file(file_path.as_posix(), output_file)

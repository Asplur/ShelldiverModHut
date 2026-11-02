import os
from PIL import Image, ImageDraw

W, H = 124, 68
OUTLINE = (32, 22, 48, 255)
WOOD_DARK = (92, 48, 54, 255)
WOOD = (134, 66, 66, 255)
WOOD_LIGHT = (176, 98, 84, 255)
ROOF_DARK = (56, 62, 112, 255)
ROOF = (86, 96, 150, 255)
ROOF_LIGHT = (128, 140, 188, 255)
WALL = (232, 200, 150, 255)
WALL_SHADE = (204, 164, 120, 255)
CHAIR = (88, 92, 140, 255)
CHAIR_DARK = (58, 60, 100, 255)
CUSHION = (200, 112, 108, 255)
CUSHION_DARK = (160, 80, 84, 255)
SCREEN = (120, 230, 240, 255)
SCREEN_DARK = (60, 170, 190, 255)
GREY = (150, 156, 170, 255)
GREY_DARK = (100, 106, 124, 255)
LEAF = (88, 170, 72, 255)
LEAF_DARK = (56, 120, 60, 255)
BOOKS = [(236, 90, 90, 255), (232, 128, 40, 255), (240, 206, 150, 255), (90, 170, 200, 255), (200, 120, 120, 255), (120, 180, 110, 255)]

img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)


def box(x0, y0, x1, y1, fill, outline=OUTLINE):
    d.rectangle([x0, y0, x1, y1], fill=fill, outline=outline)


def fill(x0, y0, x1, y1, color):
    d.rectangle([x0, y0, x1, y1], fill=color)


FLOOR_TOP = H - 7
ROOF_BOTTOM = 15

box(6, ROOF_BOTTOM, W - 7, FLOOR_TOP, WALL)
fill(7, ROOF_BOTTOM + 1, W - 8, ROOF_BOTTOM + 3, WALL_SHADE)
for x in range(16, W - 8, 10):
    d.line([x, ROOF_BOTTOM + 4, x, FLOOR_TOP - 1], fill=WALL_SHADE)
fill(7, FLOOR_TOP - 3, W - 8, FLOOR_TOP - 1, WOOD_LIGHT)
d.line([7, FLOOR_TOP - 4, W - 8, FLOOR_TOP - 4], fill=WOOD_DARK)

box(0, FLOOR_TOP, W - 1, H - 1, WOOD)
fill(1, FLOOR_TOP + 1, W - 2, FLOOR_TOP + 2, WOOD_LIGHT)
for x in range(9, W - 2, 12):
    d.line([x, FLOOR_TOP + 3, x, H - 2], fill=WOOD_DARK)

for x0 in (3, W - 9):
    box(x0, ROOF_BOTTOM, x0 + 5, FLOOR_TOP, WOOD)
    fill(x0 + 1, ROOF_BOTTOM + 1, x0 + 1, FLOOR_TOP - 1, WOOD_LIGHT)

d.polygon([(12, 1), (W - 13, 1), (W - 1, ROOF_BOTTOM), (0, ROOF_BOTTOM)], fill=ROOF, outline=OUTLINE)
d.line([(13, 2), (W - 14, 2)], fill=ROOF_LIGHT)
for x in range(16, W - 12, 12):
    d.line([(x, 3), (x - 6 + (x - 16) // 8, ROOF_BOTTOM - 1)], fill=ROOF_DARK)
box(0, ROOF_BOTTOM, W - 1, ROOF_BOTTOM + 3, ROOF_DARK)
fill(1, ROOF_BOTTOM + 1, W - 2, ROOF_BOTTOM + 1, ROOF)

box(11, 21, 33, FLOOR_TOP, WOOD)
for shelf, y in enumerate((23, 31, 39, 47)):
    fill(13, y, 31, y + 5, WOOD_DARK)
    d.line([12, y + 6, 32, y + 6], fill=OUTLINE)
    x = 13
    pick = shelf * 2
    while x < 29:
        wide = 2 + (pick % 2)
        top = y + (pick % 3)
        fill(x, top, x + wide - 1, y + 5, BOOKS[pick % len(BOOKS)])
        x += wide + (1 if pick % 4 == 3 else 0)
        pick += 1
fill(13, 55, 31, FLOOR_TOP - 1, WOOD_DARK)
fill(21, 57, 23, 58, WOOD_LIGHT)

DESK_TOP = 44
box(41, DESK_TOP, 82, DESK_TOP + 3, WOOD_LIGHT)
fill(42, DESK_TOP + 2, 81, DESK_TOP + 2, WOOD)
box(43, DESK_TOP + 3, 46, FLOOR_TOP, WOOD)
box(68, DESK_TOP + 3, 81, FLOOR_TOP, WOOD)
d.line([69, 54, 80, 54], fill=OUTLINE)
fill(73, 50, 76, 50, WOOD_DARK)
fill(73, 57, 76, 57, WOOD_DARK)

box(48, 30, 66, 41, GREY_DARK)
fill(50, 32, 64, 39, SCREEN)
fill(50, 37, 64, 39, SCREEN_DARK)
d.line([52, 34, 60, 34], fill=(240, 252, 255, 255))
d.line([52, 36, 57, 36], fill=(240, 252, 255, 255))
fill(55, 42, 59, 43, GREY_DARK)

box(70, 38, 77, DESK_TOP, WOOD_DARK)
fill(71, 39, 76, 40, BOOKS[0])
fill(71, 41, 76, 42, BOOKS[2])

box(45, 41, 47, 43, GREY)

box(53, 37, 66, 50, CHAIR)
fill(54, 38, 56, 49, CHAIR_DARK)
box(50, 49, 69, 53, CHAIR)
fill(51, 52, 68, 52, CHAIR_DARK)
fill(59, 54, 60, FLOOR_TOP - 3, OUTLINE)
d.line([52, FLOOR_TOP - 2, 67, FLOOR_TOP - 2], fill=OUTLINE)
for x in (52, 59, 67):
    fill(x - 1, FLOOR_TOP - 1, x, FLOOR_TOP, OUTLINE)

ARM = 90
box(ARM + 3, 41, ARM + 20, 52, CUSHION)
fill(ARM + 4, 42, ARM + 7, 51, CUSHION_DARK)
d.line([ARM + 11, 43, ARM + 11, 50], fill=CUSHION_DARK)
box(ARM + 1, 51, ARM + 22, FLOOR_TOP, CUSHION)
fill(ARM + 2, 56, ARM + 21, FLOOR_TOP - 1, CUSHION_DARK)
box(ARM, 47, ARM + 4, FLOOR_TOP, CUSHION)
box(ARM + 19, 47, ARM + 23, FLOOR_TOP, CUSHION)

box(102, 22, 112, 32, WOOD)
fill(104, 24, 110, 30, WALL)
d.line([105, 29, 107, 26], fill=BOOKS[0])
d.line([107, 26, 109, 28], fill=BOOKS[0])

box(36, FLOOR_TOP - 5, 39, FLOOR_TOP, WOOD_LIGHT)
fill(37, FLOOR_TOP - 8, 38, FLOOR_TOP - 6, LEAF_DARK)
fill(35, FLOOR_TOP - 11, 36, FLOOR_TOP - 8, LEAF)
fill(39, FLOOR_TOP - 10, 40, FLOOR_TOP - 8, LEAF)
fill(37, FLOOR_TOP - 13, 38, FLOOR_TOP - 9, LEAF)

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Office', 'Office.png')
os.makedirs(os.path.dirname(out), exist_ok=True)
img.save(out)

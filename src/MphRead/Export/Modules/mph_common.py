import bpy

def get_common_version():
    return '0.11.0.0'

def get_object(name):
    try:
        return bpy.data.objects[name]
    except:
        return None

def get_material(name):
    try:
        return bpy.data.materials[name]
    except:
        return None

def get_materials():
    for item in bpy.data.materials:
        yield bpy.data.materials[item.name]

def material_get_node(self, name):
    try:
        return self.node_tree.nodes[name]
    except:
        return None

def material_get_bsdf(self):
    return self.get_node('Principled BSDF')

def node_get_input(self, name):
    return self.inputs[name]

def node_get_output(self, name):
    return self.outputs[name]

def material_get_bsdf_input(self, name):
    return self.get_bsdf().get_input(name)

def material_add_node(self, kind):
    return self.node_tree.nodes.new(kind)

def material_delete_node(self, name):
    node = self.get_node(name)
    if node:
        self.node_tree.nodes.remove(node)

def material_link_nodes(self, source, output, target, input):
    if (isinstance(source, str)):
        source = self.get_node(source)
    if (isinstance(target, str)):
        target = self.get_node(target)
    self.node_tree.links.new(
        source.outputs[output],
        target.inputs[input]
    )

bpy.types.Node.get_input = node_get_input
bpy.types.Node.get_output = node_get_output
bpy.types.Material.get_node = material_get_node
bpy.types.Material.get_bsdf = material_get_bsdf
bpy.types.Material.get_bsdf_input = material_get_bsdf_input
bpy.types.Material.link_nodes = material_link_nodes
bpy.types.Material.add_node = material_add_node
bpy.types.Material.delete_node = material_delete_node

def cleanup():
    removed = get_material('Dots Stroke')
    if (removed):
        bpy.data.materials.remove(removed)
    removed = get_material('Material')
    if (removed):
        bpy.data.materials.remove(removed)
    removed = get_object('Cube')
    if (removed):
        bpy.data.objects.remove(removed)

def set_common():
    for material in get_materials():
        material.get_bsdf_input('Specular').default_value = 0
        material.shadow_method = 'NONE'
        set_vertex_colors(material)
        
def set_vertex_colors(material):
    color = material.add_node('ShaderNodeVertexColor')
    texture = material.get_node('Image Texture')
    if (texture):
        texture.interpolation = 'Closest'
        mix = material.add_node('ShaderNodeMixRGB')
        mix.blend_type = 'MULTIPLY'
        mix.get_input('Fac').default_value = 1
        material.link_nodes(
            'Vertex Color', 'Color',
            'Mix', 'Color1'
        )
        material.link_nodes(
            'Image Texture', 'Color',
            'Mix', 'Color2'
        )
        material.link_nodes(
            'Mix', 'Color',
            'Principled BSDF', 'Base Color'
        )
    else:
        material.link_nodes(
            'Vertex Color', 'Color',
            'Principled BSDF', 'Base Color'
        )
    
def set_material_alpha(name, materialAlpha):
    material = get_material(name)
    material.blend_method = 'BLEND'
    material.show_transparent_back = False
    math = material.add_node('ShaderNodeMath')
    math.operation = 'MULTIPLY'
    math.inputs[0].default_value = 1.0
    math.inputs[1].default_value = materialAlpha / 31
    material.link_nodes(
        'Math', 'Value',
        'Principled BSDF', 'Alpha'
    )
    
def set_texture_alpha(name, materialAlpha, textureAlpha):
    material = get_material(name)
    material.blend_method = 'BLEND'
    material.show_transparent_back = False
    math = material.add_node('ShaderNodeMath')
    math.operation = 'MULTIPLY'
    math.inputs[1].default_value = materialAlpha / 31
    material.link_nodes(
        'Image Texture', 'Alpha',
        'Math', 0
    )
    material.link_nodes(
        'Math', 'Value',
        'Principled BSDF', 'Alpha'
    )
        
def set_billboard(name, mode):
    obj = get_object(name)
    constraint = obj.constraints.new('LOCKED_TRACK')
    constraint.target = bpy.data.objects['Camera']
    constraint.track_axis = 'TRACK_Z'
    constraint.lock_axis = 'LOCK_Y'
    if (mode == 1):
        constraint = obj.constraints.new('LOCKED_TRACK')
        constraint.target = bpy.data.objects['Camera']
        constraint.track_axis = 'TRACK_Z'
        constraint.lock_axis = 'LOCK_X'
    
def set_back_culling(name):
    material = get_material(name)
    material.use_backface_culling = True
    
def invert_normals(name):
    if (bpy.context.object.mode != 'OBJECT'):
        bpy.ops.object.mode_set(mode = 'OBJECT')
    bpy.ops.object.select_all(action = 'DESELECT')
    bpy.context.active_object.select_set(False)
    for obj in bpy.context.selected_objects:
        bpy.context.view_layer.objects.active = None
    bpy.context.view_layer.objects.active = get_object(name)
    bpy.ops.object.mode_set(mode = 'EDIT')
    bpy.ops.mesh.select_all(action = 'SELECT')
    bpy.ops.mesh.flip_normals()
    bpy.ops.mesh.select_all(action = 'DESELECT')
    
def set_mirror(name, x, y):
    material = get_material(name)
    uvs = material.add_node('ShaderNodeUVMap')
    separate = material.add_node('ShaderNodeSeparateXYZ')
    material.link_nodes(
        uvs, 'UV',
        separate, 'Vector'
    )
    combine = material.add_node('ShaderNodeCombineXYZ')
    if (x):
        math = material.add_node('ShaderNodeMath')
        math.operation = 'PINGPONG'
        math.inputs[1].default_value = 1
        material.link_nodes(
            separate, 'X',
            math, 0
        )
        material.link_nodes(
            math, 'Value',
            combine, 'X'
        )
    else:
        material.link_nodes(
            separate, 'X',
            combine, 'X'
        )
    if (y):
        math = material.add_node('ShaderNodeMath')
        math.operation = 'PINGPONG'
        math.inputs[1].default_value = 1
        material.link_nodes(
            separate, 'Y',
            math, 0
        )
        material.link_nodes(
            math, 'Value',
            combine, 'Y'
        )
    else:
        material.link_nodes(
            separate, 'Y',
            combine, 'Y'
        )
    material.link_nodes(
        separate, 'Z',
        combine, 'Z'
    )
    material.link_nodes(
        combine, 'Vector',
        'Image Texture', 'Vector'
    )

def set_uv_anims(anims):
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            obj.modifiers.new('UVWarp', 'UV_WARP')
    bpy.context.scene.frame_start = 0
    for name, anim in anims.items():
        for obj in bpy.data.objects:
            if obj.type == 'MESH':
                mat_name = obj.active_material.name
                if mat_name == name + '_mat' or mat_name == name + '_mat_mc':
                    set_uv_anim(obj, anim)
                    for fcurve in obj.animation_data.action.fcurves:
                        for kf in fcurve.keyframe_points:
                            kf.interpolation = 'CONSTANT'
    bpy.context.scene.frame_set(0)

def set_uv_anim(obj, anim):
    mod = obj.modifiers['UVWarp']
    for i, frame in enumerate(anim):
        mod.scale[0] = frame[0]
        mod.scale[1] = frame[1]
        mod.rotation = frame[2]
        mod.offset[0] = frame[3]
        mod.offset[1] = frame[4]
        obj.keyframe_insert('modifiers["UVWarp"].scale', frame = i)
        obj.keyframe_insert('modifiers["UVWarp"].rotation', frame = i)
        obj.keyframe_insert('modifiers["UVWarp"].offset', frame = i)
    bpy.context.scene.frame_end = i

def set_mat_color(name, r, g, b, duplicate, objects):
    mat = get_material(name)
    mat_name = mat.name + '_mc'
    if duplicate:
        mat = mat.copy()
        for obj_name in objects:
            obj = get_object(obj_name)
            obj.active_material = mat
    mat.name = mat_name
    mat.delete_node('Vertex Color')
    mat.delete_node('RGB')
    rgb = mat.add_node('ShaderNodeRGB')
    color = rgb.get_output('Color')
    color.default_value[0] = r
    color.default_value[1] = g
    color.default_value[2] = b
    mat.link_nodes(
        'RGB', 'Color',
        'Mix', 'Color1'
    )

def set_mat_anims(anims):
    bpy.context.scene.frame_start = 0
    for name, anim in anims.items():
        for mat in get_materials():
            if mat.name == name or mat.name == name + '_mc':
                set_mat_anim(mat, anim)
                for fcurve in mat.node_tree.animation_data.action.fcurves:
                    for kf in fcurve.keyframe_points:
                        kf.interpolation = 'CONSTANT'
    bpy.context.scene.frame_set(0)

def set_mat_anim(mat, anim):
    for i, frame in enumerate(anim):
        rgb = mat.get_node('RGB')
        if rgb:
            color = rgb.get_output('Color')
            color.default_value[0] = frame[0]
            color.default_value[1] = frame[1]
            color.default_value[2] = frame[2]
            color.keyframe_insert('default_value', frame = i)
        alpha = mat.get_node('Math')
        alpha.inputs[1].default_value = frame[3]
        alpha.inputs[1].keyframe_insert('default_value', frame = i)
    bpy.context.scene.frame_end = i

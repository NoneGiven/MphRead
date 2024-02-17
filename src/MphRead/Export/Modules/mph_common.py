import bpy
import mathutils

def get_min_version():
    return '0.23.0.0'

def get_common_version():
    # moved to min version support in 0.23.0.0
    raise Exception(("Import script version is less than 0.23.0.0 and unsupported by current mph_common."
            " Re-export the model with a later version of MphRead."))

def validate_version(version_str):
    min_version_str = get_min_version()
    min_version = parse_version(min_version_str)
    version = parse_version(version_str)
    if (version[0] < min_version[0]
        or version[0] = min_version[0] and version[1] < min_version[1]
        or version[0] = min_version[0] and version[1] = min_version[1] and version[2] < min_version[2]
        or version[0] = min_version[0] and version[1] = min_version[1] and version[2] = min_version[2] and version[3] < min_version[3]
    ):
        raise Exception((f"Import script version {version_str} is lower than minimum version {min_version_str} supported by current mph_common."
            " Re-export the model with a later version of MphRead."))

def parse_version(version):
    split = version.split('.')
    return [int(split[0]), int(split[1]), int(split[2]), int(split[3])]

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
        for node in self.node_tree.nodes:
            if node.bl_idname == name:
                return node                            
    except:
        return None

def material_get_bsdf(self):
    return self.get_node('ShaderNodeBsdfPrincipled')

def node_get_input(self, *names):
    for name in names:
        if name in self.inputs:
            return self.inputs[name]
    return None

def node_get_output(self, *names):
    for name in names:
        if name in self.outputs:
            return self.outputs[name]
    return None

def material_get_bsdf_input(self, *names):
    return self.get_bsdf().get_input(*names)

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
        material.get_bsdf_input('Specular IOR Level', 'Specular').default_value = 0
        material.shadow_method = 'NONE'
        set_vertex_colors(material)
        
def set_vertex_colors(material):
    color = material.add_node('ShaderNodeVertexColor')
    texture = material.get_node('ShaderNodeTexImage')
    if (texture):
        texture.interpolation = 'Closest'
        mix = material.add_node('ShaderNodeMixRGB')
        mix.blend_type = 'MULTIPLY'
        mix.get_input('Fac').default_value = 1
        material.link_nodes(
            color, 'Color',
            mix, 'Color1'
        )
        material.link_nodes(
            texture, 'Color',
            mix, 'Color2'
        )
        material.link_nodes(
            mix, 'Color',
            'ShaderNodeBsdfPrincipled', 'Base Color'
        )
    else:
        material.link_nodes(
            color, 'Color',
            'ShaderNodeBsdfPrincipled', 'Base Color'
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
        math, 'Value',
        'ShaderNodeBsdfPrincipled', 'Alpha'
    )
    
def set_texture_alpha(name, materialAlpha, textureAlpha):
    material = get_material(name)
    material.blend_method = 'BLEND'
    material.show_transparent_back = False
    math = material.add_node('ShaderNodeMath')
    math.operation = 'MULTIPLY'
    math.inputs[1].default_value = materialAlpha / 31
    material.link_nodes(
        'ShaderNodeTexImage', 'Alpha',
        math, 0
    )
    material.link_nodes(
        math, 'Value',
        'ShaderNodeBsdfPrincipled', 'Alpha'
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
        'ShaderNodeTexImage', 'Vector'
    )

def set_uv_anims(anims):
    bpy.context.scene.frame_start = 0
    for name, anim in anims.items():
        for mat in get_materials():
            if mat.name == name + '_mat' or mat.name == name + '_mat_mc':
                set_uv_anim(mat, anim)
                for fcurve in mat.node_tree.animation_data.action.fcurves:
                    for kf in fcurve.keyframe_points:
                        kf.interpolation = 'CONSTANT'
    bpy.context.scene.frame_set(0)

def set_uv_anim(mat, anim):
    mat.delete_node('ShaderNodeUVMap')
    uvs = mat.add_node('ShaderNodeUVMap')
    rotate = mat.add_node('ShaderNodeVectorRotate')
    translate = mat.add_node('ShaderNodeVectorMath')
    scale = mat.add_node('ShaderNodeVectorMath')
    rotate.rotation_type = 'EULER_XYZ'
    translate.operation = 'ADD'
    scale.operation = 'MULTIPLY'
    rot_center = rotate.get_input('Center')
    rot_center.default_value[0] = 0.5
    rot_center.default_value[1] = 0.5
    rot_input = rotate.get_input('Rotation')
    translate_input = translate.inputs[1]
    scale_input = scale.inputs[1]
    scale_input.default_value[2] = 1.0
    mat.link_nodes(
        uvs, 'UV',
        rotate, 'Vector'
    )
    mat.link_nodes(
        rotate, 'Vector',
        translate, 'Vector'
    )
    mat.link_nodes(
        translate, 'Vector',
        scale, 'Vector'
    )
    if mat.get_node('ShaderNodeSeparateXYZ'):
        mat.link_nodes(
            scale, 'Vector',
            'ShaderNodeSeparateXYZ', 'Vector'
        )
    else:
        mat.link_nodes(
            scale, 'Vector',
            'ShaderNodeTexImage', 'Vector'
        )
    for i, frame in enumerate(anim):
        scale_input.default_value[0] = frame[0]
        scale_input.default_value[1] = frame[1]
        rot_input.default_value[2] = frame[2] * -1.0
        translate_input.default_value[0] = frame[3]
        translate_input.default_value[1] = frame[4] * -1.0
        scale_input.keyframe_insert('default_value', frame = i)
        rot_input.keyframe_insert('default_value', frame = i)
        translate_input.keyframe_insert('default_value', frame = i)
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
    mat.delete_node('ShaderNodeVertexColor')
    mat.delete_node('ShaderNodeRGB')
    rgb = mat.add_node('ShaderNodeRGB')
    color = rgb.get_output('Color')
    color.default_value[0] = r
    color.default_value[1] = g
    color.default_value[2] = b
    if mat.get_node('ShaderNodeMixRGB'):
        mat.link_nodes(
            rgb, 'Color',
            'ShaderNodeMixRGB', 'Color1'
        )
    else:
        mat.link_nodes(
            rgb, 'Color',
            'ShaderNodeBsdfPrincipled', 'Base Color'
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
        rgb = mat.get_node('ShaderNodeRGB')
        if rgb:
            color = rgb.get_output('Color')
            color.default_value[0] = frame[0]
            color.default_value[1] = frame[1]
            color.default_value[2] = frame[2]
            color.keyframe_insert('default_value', frame = i)
        alpha = mat.get_node('ShaderNodeMath')
        alpha.inputs[1].default_value = frame[3]
        alpha.inputs[1].keyframe_insert('default_value', frame = i)
    bpy.context.scene.frame_end = i

def set_tex_anims(anims):
    bpy.context.scene.frame_start = 0
    for name, anim in anims.items():
        for mat in get_materials():
            if mat.name == name or mat.name == name + '_mc':
                set_tex_anim(mat, anim)
    bpy.context.scene.frame_set(0)

def set_tex_anim(mat, anim):
    max_frame = 0
    tex = mat.get_node('ShaderNodeTexImage')
    tex.image = bpy.data.images['anim__001.png'].copy()
    tex.image.source = 'SEQUENCE'
    tex.image_user.frame_duration = 1
    tex.image_user.frame_start = 1
    tex.image_user.use_cyclic = True
    tex.image_user.use_auto_refresh = True
    for frame in anim:
        tex.image_user.frame_offset = frame[1]
        tex.image_user.keyframe_insert('frame_offset', frame = frame[0])
        if frame[0] > max_frame:
            max_frame = frame[0]
    bpy.context.scene.frame_end = max_frame

def set_node_anims(anims):
    bpy.context.scene.frame_start = 0
    for name, anim in anims.items():
        bone = bpy.data.objects['Armature'].pose.bones[name]
        set_node_anim(bone, anim)
    for fcurve in bpy.data.objects['Armature'].animation_data.action.fcurves:
        for kf in fcurve.keyframe_points:
            kf.interpolation = 'CONSTANT'
    bpy.context.scene.frame_set(0)
    bpy.data.objects['Armature'].display_type = 'WIRE'

def set_node_anim(bone, anim):
    for i, frame in enumerate(anim):
        bone.scale = mathutils.Vector((frame[0], frame[1], frame[2]))
        bone.rotation_euler = mathutils.Vector((frame[3], frame[4], frame[5]))
        bone.location = mathutils.Vector((frame[6], frame[7], frame[8]))
        bone.keyframe_insert('scale', frame = i)
        bone.keyframe_insert('rotation_euler', frame = i)
        bone.keyframe_insert('location', frame = i)
    bpy.context.scene.frame_end = i

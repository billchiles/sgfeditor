### sgfparser.py parses .sgf files.  Each parse takes a file and creates a list
### of ParsedNodes.  The list of nodes may not adhere to normal game moves such
### as alternating colors, or starting with B in an even game and W with
### handicaps.  The first node is the root node and should be game properties
### while following nodes should represent a game, but the nodes could
### represent setup for a problem.
###

import re
from System.IO import FileFormatException



class ParsedGame (object):
    def __init__ (self):
        ## nodes is the only public member.
        self.nodes = None

    ### __str__ produces a strong that when printed to a file generates a valid
    ### .sgf file.
    ###
    def __str__ (self):
        if self.nodes is None:
            return ""  ## Min tree is "(;)", but that implies one empty node
        else:
            return "(" + self._nodes_string(self.nodes) + ")"
    
    ### _nodes_string returns a string for a series of nodes, and the caller
    ### needs to supply the open and close parens that bracket the series.
    ###
    def _nodes_string (self, nodes):
        res = ""
        while nodes.next is not None:
            ## Get one node's string with a leading newline if it is not the
            ## first.
            res += nodes.node_str(res != "")
            if nodes.branches is not None:
                for n in nodes.branches:
                    res = res + "\n(" + self._nodes_string(n) + ")"
                return res
            nodes = nodes.next
        res += nodes.node_str(res != "") # Test res, could be single node branch.
        return res
    



class ParsedNode (object):
    def __init__ (self):
        self.next = None
        self.previous = None
        self.branches = None
        self.properties = {}
    
    ### node_str returns the string for one node, taking a flag for a
    ### preceding newline and the dictionary of properties for the node.
    ###
    def node_str (self, newline):
        props = self.properties
        if newline:
            s = "\n;"
        else:
            s = ";"
        ## Print move property first for readability of .sgf file by humans.
        if "B" in props:
            s = s + "B" + self._escaped_property_values("B", props["B"])
        if "W" in props:
            s = s + "W" + self._escaped_property_values("W", props["W"])
        for k,v in props.iteritems():
            if k == "B" or k == "W": continue
            s = s + k + self._escaped_property_values(k, v)
        return s

    ### _escaped_property_values returns a node's property value with escapes so that the .sgf
    ### is valid.  So, ] and \ must be preceded by a backslash.
    ###
    def _escaped_property_values (self, id, values):
        res = ""
        for v in values:
            res = res + "["
            if "]" in v or "\\" in v:
                tmp = []
                for c in v:
                    if c == "]" or c == "\\":
                        tmp.append("\\")
                    tmp.append(c)
                tmp.append("]")
                res = res + "".join(tmp)
            else:
                res = res + v + "]"
        return res



def parse_file (name):
    f = open(name)
    l = Lexer(f.read())
    f.close()
    l.scan_for("(", "Can't find game start")
    g = ParsedGame()
    g.nodes = _parse_nodes(l)
    return g

### _parse_nodes returns a linked list of ParseNodes.  It starts scanning for a
### semi-colon for the start of the first node.  If it encounters an open
### paren, it recurses and creates branches that follow the current node,
### making the next pointer of the current node point to the first node in the
### first branch.
###
def _parse_nodes (lexer):
    lexer.scan_for(";", "Must be one node in each branch")
    cur_node = _parse_node(lexer)
    first = cur_node
    branching_yet = False
    while lexer.has_data():
        ## Semi-colon starts another node, open paren starts a branch, close
        ## paren stops list of nodes.  Scanning raises an exception if one of
        ## these chars fails to follow (ignoring whitespace).
        char = lexer.scan_for(";()")
        if char == ";":
            if branching_yet:
                raise Exception("Found node after branching started.")
            cur_node.next = _parse_node(lexer)
            cur_node.next.previous = cur_node
            cur_node = cur_node.next
        elif char == "(":
            if not branching_yet:
                cur_node.next = _parse_nodes(lexer)
                cur_node.next.previous = cur_node
                cur_node.branches = [cur_node.next]
                branching_yet = True
            else:
                n = _parse_nodes(lexer)
                n.previous = cur_node
                cur_node.branches.append(n)
        elif char == ')':
            return first
        else:
            raise FileFormatException("SGF file is malformed at char " + str(lexer.Location))
    raise FileFormatException("Unexpectedly hit EOF!")

### _parse_node returns a ParseNode with its properties filled in.
###
def _parse_node (lexer):
    node = ParsedNode()
    ## Loop properties ...
    while lexer.has_data():
        id = lexer.get_property_id()
        if not id:
            return node
        if node.properties.has_key(id):
            raise Exception("Encountered ID, %s, twice for node -- file location %s." %
                            (id, lexer.location))
        lexer.scan_for("[", "Expected property value")
        i = None
        values = []
        node.properties[id] = values
        ## Loop values for one property
        while lexer.has_data():
            ## C and GC properties allow newline sequences in value.
            values.append(lexer.get_property_value(id == "C" or id == "GC"))
            ## Must bind ignore due to Python's multi-value return model.
            i, ignore = lexer.peek_for("[")
            if i is None: break #no new values
            lexer.set_location(i)
    raise FileFormatException("Unexpectedly hit EOF!")



class Lexer (object):
    def __init__ (self, contents):
        self._data = contents
        self._data_len = len(contents)
        self._index = 0
        self._put_token = None
    
    ### scan_for scans for any char in chars following whitespace.  If
    ### non-whitespace intervenes, this is an error.  Scan_for leaves _index
    ### after char and returns found char.
    ###
    def scan_for (self, chars, errmsg = None):
        i, c = self.peek_for(chars)
        if i is None:
            if errmsg:
                errmsg = errmsg + " -- file location %s" % self._index
            raise Exception(errmsg or "Expecting one of '%s' while scanning -- file location %s" % (chars, self._index))
        else:
            self._index = i
            return c

    ### peek_for scans for any char in chars following whitespace.  If
    ### non-whitespace intervenes, this is an error.  Peek_for leaves _index
    ### unmodified.
    ###
    def peek_for (self, chars):
        i = self._index
        while self.has_data():
            c = self._data[i]
            i += 1
            if c in " \t\n\r\f\v":
                continue
            elif c in chars:
                return (i, c)
            else:
                return (None, None)
        return (None, None)
        
    def has_data (self):
        return self._index < self._data_len
    
    def location (self):
        return self._index
    
    def set_location (self, i):
        self._index = i
        return i
    
    _property_id_regexp = re.compile(r'\s*([A-Za-z]+)')
    
    ### "text" properties can have newlines, newlines following \ are removed
    ### along with \, other escaped chars are kept verbatim except whitespace
    ### is converted to space.
    ###
    ### "simpletext" is the same as "text" but has no newlines.
    ###
    def get_property_id (self):
        match = self._property_id_regexp.match(self._data, self._index)
        if match:
            self._index = match.end()
            return match.group(1)
        return None

    ### get_property_value takes a flag as to whether un-escaped newlines get
    ### mapped to space or kept as-is.  It gobbles all the characters after a
    ### '[' (which has already been consumed) up to the next ']' and returns
    ### them as a string.  Keep_newlines distinguishes properties like C and GC
    ### that can have newlines in their values, but otherwise, newlines are
    ### assumed to be purely line-length management in the .sgf file.
    ###
    def get_property_value (self, keep_newlines):
        res = []
        while self.has_data():
            c = self._data[self._index]
            self._index += 1
            if ord(c) < ord(' '): #if < space
                ## Map whitespace to spaces.
                newline, c2 = self._check_property_newline(c)
                if newline:
                    ## Only map newline sequences according to keep_newlines.
                    if keep_newlines:
                        res.append(c)
                        if c2 is not None:
                            res.append(c2)
                    else:
                        res.append(" ")
                else:
                    res.append(" ")
            elif c == '\\':
                ## Backslash quotes chars and erases newlines.
                c = self._data[self._index]
                self._index += 1
                newline, ignore = self._check_property_newline(c)
                if newline:
                    res.append("")
                else:
                    res.append(c)
            elif c == "]":
                return "".join(res)
            else:
                res.append(c)
        raise FileFormatException("Unexpectedly hit EOF!")

    ### _check_property_newline check if c is part of a newline sequence.  If
    ### it is, then see if there's a second newline sequence char and gobble
    ### it.  Returns whether there was a newline sequence and what the second
    ### char was if it was part of the newline sequence.
    ###
    def _check_property_newline (self, c):
        if c == '\n' or c == '\r':
            ## Only map newline sequences according to keep_newlines.
            c2 = self._data[self._index]
            if c2 == '\n' or c2 == '\r':
                self._index += 1
                return (True, c2)
            return (True, None)
        else:
            return (False, None)


